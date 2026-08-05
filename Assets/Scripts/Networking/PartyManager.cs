using System.Collections;
using UnityEngine;

/// <summary>
/// Scene-side coordinator for the party feature, living on the existing "PartyManager"
/// GameObject under Managers. It owns NO party state — that is the replicated
/// <see cref="PartyLobby"/> — it only reacts to networked party events and drives
/// scene-level concerns the networking layer should not know about:
///   • Making sure the invite popup listener exists (24.4 receive side).
///   • Auto-opening the party panel for a member the moment they join a party, so an
///     invited friend sees the roster without manually opening any menu (24.6).
///   • Bridging "host pressed Start" into the actual dungeon load + migration (Step 5).
///
/// This keeps <see cref="UIPartyPanel"/> a pure view and <see cref="PartyService"/>
/// a pure command facade — the reactive glue lives here.
/// </summary>
public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    /// <summary>
    /// True from the moment a dungeon entry starts until the dungeon scene transition is
    /// kicked off. In that window the PartyLobby has already despawned (the host tore the
    /// lobby runner down) but the dungeon room is not up yet, so neither party transport
    /// is live. Party-facing UI must not conclude "you are not in a party" here — the
    /// party is intact, it is only mid-migration.
    /// </summary>
    public static bool IsEnteringDungeon { get; private set; }

    private PartyLobby _hookedParty;

    // Guards so the one-shot dungeon-entry work runs exactly once per transition.
    private bool _hostStartInProgress;
    private bool _dungeonEntryStarted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // Ensure the invite popup exists even if we entered Main without the bootstrap
        // path (e.g. domain reload in editor).
        PartyInvitePopup.EnsureExists();

        PartyLobby.OnLocalPartyChanged += HandleLocalPartyChanged;
        RehookParty();
    }

    private void OnDisable()
    {
        PartyLobby.OnLocalPartyChanged -= HandleLocalPartyChanged;
        UnhookParty();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Party event wiring
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleLocalPartyChanged()
    {
        RehookParty();

        // A member who just joined a party (not the host, who opened the panel itself)
        // should see the roster pop up automatically.
        var party = PartyLobby.Local;
        if (party != null && !party.IsLocalHost)
        {
            OpenPartyPanelForMember();
        }
    }

    private void RehookParty()
    {
        UnhookParty();
        _hookedParty = PartyLobby.Local;
        if (_hookedParty != null)
        {
            _hookedParty.OnDungeonStartRequested += HandleDungeonStartRequested;
            _hookedParty.OnPartyStateChanged += HandlePartyState;
        }
        else
        {
            // No local party (fresh, left, or migrated away) — arm for the next run.
            _hostStartInProgress = false;
            _dungeonEntryStarted = false;
        }
    }

    private void UnhookParty()
    {
        if (_hookedParty != null)
        {
            _hookedParty.OnDungeonStartRequested -= HandleDungeonStartRequested;
            _hookedParty.OnPartyStateChanged -= HandlePartyState;
            _hookedParty = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Member auto-open
    // ─────────────────────────────────────────────────────────────────────────

    private void OpenPartyPanelForMember()
    {
        // Resolve the panel GameObject WITHOUT relying on UIPartyPanel.Instance: the
        // panel starts inactive in the scene, so its Awake (which sets Instance) has not
        // run yet for an invited member who never opened it manually. UIManager holds a
        // direct reference regardless of active state.
        GameObject panelGo = null;
        if (UIPartyPanel.Instance != null) panelGo = UIPartyPanel.Instance.gameObject;
        else if (UIManager.Instance != null) panelGo = UIManager.Instance.dungeonPanel;
        if (panelGo == null)
        {
            Debug.LogWarning("[PartyManager] Cannot open party panel for member — no panel reference (UIManager.dungeonPanel unset?).");
            return;
        }
        if (panelGo.activeInHierarchy) return; // already open

        // Members don't pick the dungeon; the host publishes it. Open the panel so it
        // renders the roster + the host's chosen dungeon (which arrives via networked
        // properties). We route through UIManager so it behaves like any other panel.
        var party = PartyLobby.Local;
        int configId = party != null ? party.DungeonConfigId : 1;
        string scene = party != null ? party.DungeonSceneName.Value : string.Empty;
        string name = party != null ? party.DungeonName.Value : "Dungeon";

        if (UIManager.Instance != null)
            UIManager.Instance.ShowPanel(panelGo);
        else
            panelGo.SetActive(true);

        // Awake has now run → the component's Instance is set. Fetch it off the GameObject.
        var panel = panelGo.GetComponent<UIPartyPanel>();
        if (panel != null)
            panel.OpenForDungeon(configId, string.IsNullOrEmpty(scene) ? "HollowCryptDungeon" : scene, 0, string.IsNullOrEmpty(name) ? "Dungeon" : name);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Dungeon start bridge (Step 5)
    //
    // Flow:
    //   Host: OnDungeonStartRequested (State just flipped to Loading)
    //         → create backend session (Enter API, once)
    //         → publish session id → State flips to InDungeon.
    //   Everyone: OnPartyStateChanged(InDungeon)
    //         → migrate Photon runner into the dungeon room (avatars spawn)
    //         → wait for the local avatar, then load the dungeon scene.
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleDungeonStartRequested(int configId, string sceneName)
    {
        var party = PartyLobby.Local;
        if (party == null || !party.IsLocalHost) return; // only the host creates the session
        if (_hostStartInProgress) return;
        _hostStartInProgress = true;

        if (DungeonManager.Instance == null)
        {
            Debug.LogError("[PartyManager] DungeonManager missing — cannot start party dungeon.");
            _hostStartInProgress = false;
            return;
        }

        string dungeonName = party.DungeonName.Value;
        int cost = 0; // energy already validated in the panel; party members share the run

        DungeonManager.Instance.CreatePartySession(
            configId, sceneName, cost, dungeonName, BuildPartyMemberIds(party),
            sessionId =>
            {
                if (sessionId <= 0)
                {
                    // Backend rejected the Enter call (e.g. insufficient energy, invalid
                    // party composition). Revert PartyState so the host can retry instead
                    // of orphaning the party in Loading forever.
                    Debug.LogWarning("[PartyManager] CreatePartySession failed — reverting party to Lobby.");
                    if (party != null && party.HasStateAuthority)
                    {
                        party.RevertToLobby();
                    }
                    _hostStartInProgress = false;
                    return;
                }
                // Publishing the session id flips PartyState → InDungeon, which drives
                // the migration on EVERY client (including this host, via HandlePartyState).
                party.HostPublishDungeonSession(sessionId);
                _hostStartInProgress = false;
            });
    }

    private void HandlePartyState(PartyLobby.PartyState state)
    {
        Debug.Log($"[PartyEntry] HandlePartyState({state}) fired. entryStarted={_dungeonEntryStarted} " +
                  $"isHost={(PartyLobby.Local != null ? PartyLobby.Local.IsLocalHost.ToString() : "no-local")}");
        if (state != PartyLobby.PartyState.InDungeon || _dungeonEntryStarted) return;

        var party = PartyLobby.Local;
        if (party == null)
        {
            // Not seated (shouldn't happen for a real member). Leave the flag unset so a
            // later state re-notification can still start the entry.
            Debug.LogWarning("[PartyEntry] InDungeon fired but PartyLobby.Local is null — cannot snapshot yet.");
            return;
        }

        // Snapshot the networked target SYNCHRONOUSLY, right now. The PartyLobby object
        // is owned by the host; the moment the host tears down its lobby runner to
        // migrate, this object is despawned for everyone and PartyLobby.Local goes null.
        // Reading it later inside the coroutine would abort the member's entry. Fusion
        // delivers all networked props of a snapshot together, so when State==InDungeon
        // is visible the session id / scene set in HostPublishDungeonSession are too.
        _dungeonEntryStarted = true;
        IsEnteringDungeon = true;
        StartCoroutine(EnterDungeonRoutine(
            party.DungeonConfigId,
            party.DungeonSceneName.Value,
            party.DungeonName.Value,
            party.DungeonSessionId,
            party.HostProfileId,
            party.IsLocalHost));
    }

    /// <summary>
    /// Runs on every client once the party state reaches InDungeon: migrate the Photon
    /// runner into the dungeon room, wait for the networked avatar to exist, then run
    /// the scene transition (reusing the host's session id — no duplicate Enter API).
    /// All target values are snapshotted by the caller so this never touches the
    /// (possibly-despawned) PartyLobby.
    /// </summary>
    private IEnumerator EnterDungeonRoutine(int configId, string scene, string dungeonName,
                                            int sessionId, int hostProfileId, bool isHost)
    {
        Debug.Log($"[PartyEntry] Snapshot | isHost={isHost} configId={configId} scene='{scene}' " +
                  $"sessionId={sessionId} hostProfileId={hostProfileId}");

        if (string.IsNullOrEmpty(scene))
        {
            Debug.LogError("[PartyEntry] ABORT: Dungeon scene name empty.");
            _dungeonEntryStarted = false;
            IsEnteringDungeon = false;
            yield break;
        }

        if (sessionId <= 0)
        {
            Debug.LogWarning("[PartyEntry] ABORT: invalid session id (host Enter call failed).");
            _dungeonEntryStarted = false;
            IsEnteringDungeon = false;
            yield break;
        }

        // Capture the world return point NOW, before migrating. Migration destroys the
        // world avatar and spawns a fresh networked one elsewhere, so DungeonManager
        // cannot read the real "where I was in the world" position afterwards.
        // WorldState.LastPosition is kept current by PlayerWorldPositionSync while in
        // the world, so it is the reliable pre-migration source.
        string returnMap = string.IsNullOrWhiteSpace(WorldState.CurrentMapName) ? "ElfForest" : WorldState.CurrentMapName;
        Vector3 returnPos = WorldState.LastPosition;

        // Che màn hình NGAY từ đầu, không đợi tới TransitionToDungeon. Từ đây tới đó là
        // 3s (host) hoặc tới 7s (member) chờ tear-down, rồi DestroyNonNetworkedPlayers()
        // xoá avatar world, rồi tới 10s chờ avatar network — tức người chơi nhìn map cũ
        // KHÔNG CÒN nhân vật suốt 3-17s. Đây là toàn bộ lý do phải Show ở đây.
        //
        // Scene "Loading" load additive và PassThroughSceneManager (scene manager của
        // runner dungeon) không unload scene nào, nên nó sống qua migration.
        // LoadingScreen.Show() trong TransitionToDungeon sau đó tự no-op vì scene đã loaded,
        // và Hide() ở cuối TransitionToDungeon mới là chỗ tắt.
        yield return LoadingScreen.Show("Entering dungeon...");

        string dungeonRoom = $"DUNGEON_{hostProfileId}";

        // Master-client election: whoever joins the fresh dungeon room FIRST becomes the
        // Fusion Shared-Mode master client, which owns enemy AI (DungeonSpawner spawns
        // only on PhotonManager.IsHost). We want the PARTY HOST to be that master, so the
        // host migrates immediately and members only follow once the host is on its way.
        // Both already snapshotted their target above, so the PartyLobby despawning
        // mid-transition is harmless.
        if (!isHost)
        {
            Debug.Log($"[PartyEntry] Member waiting for host {hostProfileId} to tear down lobby runner...");
            float wait = 6f;
            while (wait > 0f && PlayerPresence.Find(hostProfileId) != null)
            {
                wait -= Time.deltaTime;
                yield return null;
            }
            Debug.Log($"[PartyEntry] Wait loop finished. Time left: {wait}. PlayerPresence null? {(PlayerPresence.Find(hostProfileId) == null)}");

            yield return new WaitForSeconds(1f);
            Debug.Log($"[PartyEntry] Member grace period finished. Proceeding to migrate.");
        }
        else
        {
            // Give Fusion time to propagate the InDungeon state to all members
            // before tearing down the lobby runner. Without this, the host disconnects
            // too quickly and members never receive the state change.
            yield return new WaitForSeconds(3.0f);
        }

        // The lobby avatar is a NON-networked PlayerMovement left over from the world
        // scene. It must be gone before we wait for the dungeon avatar, otherwise
        // FindFirstObjectByType<PlayerMovement> below matches the stale one instantly
        // and we transition before the real networked avatar spawns (host ends up in
        // the scene with no camera-bound, correctly-placed character).
        DestroyNonNetworkedPlayers();

        var photon = PhotonManager.Instance;
        Debug.Log($"[PartyEntry] Destroyed non-networked players. PhotonManager exists? {(photon != null)}");
        if (photon != null)
        {
            var task = photon.MigrateToDungeonAsync(dungeonRoom);
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
            {
                Debug.LogError($"[PartyEntry] MigrateToDungeonAsync FAULTED: {task.Exception?.GetBaseException().Message}");
                _dungeonEntryStarted = false;
                IsEnteringDungeon = false;
                yield return AbortEntry("Could not reach the dungeon. Please try again.");
                yield break;
            }

            Debug.Log($"[PartyEntry] Migrated into '{dungeonRoom}'. IsHost={photon.IsHost} Phase={photon.Phase}");
        }
        else
        {
            Debug.LogWarning("[PartyEntry] PhotonManager.Instance is null — cannot migrate.");
        }

        // 2. Wait for the local NETWORKED avatar (NetworkPlayer.Local) to be spawned by
        //    PhotonManager on migration (or a short timeout as a safety net so we never
        //    hang on the loading state).
        float timeout = 10f;
        while (timeout > 0f && NetworkPlayer.Local == null)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (NetworkPlayer.Local == null)
            Debug.LogWarning("[PartyEntry] Timed out waiting for NetworkPlayer.Local — entering scene anyway.");
        else
            Debug.Log("[PartyEntry] NetworkPlayer.Local found! Entering Dungeon Scene.");

        // 3. Perform the scene transition using the shared session id (no Enter API here).
        // The dungeon room is up at this point, so party chat has a live transport again.
        IsEnteringDungeon = false;

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.EnterDungeonScene(configId, scene, 0, dungeonName, sessionId,
                hasReturnPoint: true, returnMapName: returnMap, returnPosition: returnPos, isHost: isHost);
        }
        else
        {
            Debug.LogError("[PartyEntry] DungeonManager.Instance is null — cannot enter scene.");
            yield return AbortEntry("Could not enter the dungeon. Please try again.");
        }
    }

    /// <summary>
    /// Tắt màn hình loading và báo lỗi khi entry chết SAU khi đã Show. Bắt buộc phải có:
    /// backdrop của scene Loading là full-screen alpha 1 nên nếu chỉ `yield break` thì
    /// người chơi ngồi trước một màn che kín vĩnh viễn, không input nào thoát được — tệ
    /// hơn hẳn so với lỗi gốc là thấy map trống.
    ///
    /// Đường thành công KHÔNG gọi hàm này: TransitionToDungeon tự Hide() ở cuối.
    /// </summary>
    private static IEnumerator AbortEntry(string message)
    {
        yield return LoadingScreen.Hide();

        // Cùng kênh DungeonManager.NotifyBlocked dùng cho entry bị chặn.
        // (WorldRuntimeEvents.RaiseMessage không có subscriber nào — sẽ im lặng.)
        if (MainQuestPanelRuntime.Instance != null)
            MainQuestPanelRuntime.Instance.ShowQuestPopup(message, UIQuestPopupView.QuestPopupKind.None);
    }

    /// <summary>
    /// Destroy any NON-networked player avatars (the world-scene player spawned by
    /// PlayerSpawner) before dungeon migration. Networked avatars (with a Fusion
    /// NetworkObject) are left untouched — Fusion owns their lifecycle. Without this,
    /// the stale lobby avatar makes the "wait for avatar" check pass instantly and we
    /// transition before the real dungeon avatar exists.
    /// </summary>
    private static void DestroyNonNetworkedPlayers()
    {
        var movers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var mover in movers)
        {
            if (mover == null) continue;
            if (mover.GetComponent<Fusion.NetworkObject>() == null)
                Destroy(mover.gameObject);
        }
    }

    /// <summary>
    /// Non-host member names for the backend Enter call. The backend validates party
    /// size as <c>1 + partyMembers.Count</c> (the "1" being the host/owner), so the host
    /// must NOT be included here or it gets double-counted and Enter is rejected for
    /// exceeding MaxMembers.
    /// </summary>
    private static System.Collections.Generic.List<string> BuildPartyMemberIds(PartyLobby party)
    {
        var ids = new System.Collections.Generic.List<string>();
        for (int i = 0; i < PartyLobby.MaxMembers; i++)
        {
            var m = party.Members[i];
            if (m.IsOccupied && m.Player != party.HostPlayer) ids.Add(m.ProfileId.ToString());
        }
        return ids;
    }
}
