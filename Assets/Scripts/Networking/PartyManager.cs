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
        var panel = UIPartyPanel.Instance;
        if (panel == null) return;
        if (panel.gameObject.activeInHierarchy) return; // already open

        // Members don't pick the dungeon; the host publishes it. Open the panel so it
        // renders the roster + the host's chosen dungeon (which arrives via networked
        // properties). We route through UIManager so it behaves like any other panel.
        var party = PartyLobby.Local;
        int configId = party != null ? party.DungeonConfigId : 1;
        string scene = party != null ? party.DungeonSceneName.Value : string.Empty;
        string name = party != null ? party.DungeonName.Value : "Dungeon";

        if (UIManager.Instance != null)
            UIManager.Instance.ShowPanel(panel.gameObject);
        else
            panel.gameObject.SetActive(true);

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
            configId, sceneName, cost, dungeonName, BuildPartyMemberNames(party),
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
        if (state == PartyLobby.PartyState.InDungeon && !_dungeonEntryStarted)
        {
            _dungeonEntryStarted = true;
            StartCoroutine(EnterDungeonRoutine());
        }
    }

    /// <summary>
    /// Runs on every client once the party state reaches InDungeon: migrate the Photon
    /// runner into the dungeon room, wait for the networked avatar to exist, then run
    /// the scene transition (reusing the host's session id — no duplicate Enter API).
    /// </summary>
    private IEnumerator EnterDungeonRoutine()
    {
        var party = PartyLobby.Local;
        if (party == null)
        {
            _dungeonEntryStarted = false;
            yield break;
        }

        // Snapshot the networked target BEFORE migrating (Local is cleared when the
        // lobby runner shuts down).
        int configId = party.DungeonConfigId;
        string scene = party.DungeonSceneName.Value;
        string dungeonName = party.DungeonName.Value;
        int sessionId = party.DungeonSessionId;
        int hostProfileId = party.HostProfileId;

        if (string.IsNullOrEmpty(scene))
        {
            Debug.LogError("[PartyManager] Dungeon scene name empty — aborting party entry.");
            _dungeonEntryStarted = false;
            yield break;
        }

        string dungeonRoom = $"DUNGEON_{hostProfileId}";

        // 1. Migrate the Photon runner into the dungeon room (spawns avatars for all).
        //    Guard: a session id <= 0 means CreatePartySession failed on the host and
        //    RevertToLobby() rolled the state back — nothing to migrate into.
        if (sessionId <= 0)
        {
            Debug.LogWarning("[PartyManager] EnterDungeonRoutine aborted: invalid session id (host Enter call failed).");
            _dungeonEntryStarted = false;
            yield break;
        }

        var photon = PhotonManager.Instance;
        if (photon != null)
        {
            var task = photon.MigrateToDungeonAsync(dungeonRoom);
            while (!task.IsCompleted) yield return null;
        }

        // 2. Wait for the local networked avatar to be spawned by PhotonManager (or a
        //    short timeout as a safety net so we never hang on the loading state).
        float timeout = 8f;
        while (timeout > 0f && FindFirstObjectByType<PlayerMovement>() == null)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // 3. Perform the scene transition using the shared session id (no Enter API here).
        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.EnterDungeonScene(configId, scene, 0, dungeonName, sessionId);
        }
    }

    private static System.Collections.Generic.List<string> BuildPartyMemberNames(PartyLobby party)
    {
        var names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < PartyLobby.MaxMembers; i++)
        {
            var m = party.Members[i];
            if (m.IsOccupied) names.Add(m.Name.Value);
        }
        return names;
    }
}
