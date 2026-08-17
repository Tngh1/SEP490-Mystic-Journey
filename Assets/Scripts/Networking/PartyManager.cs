using System.Collections;
using UnityEngine;

// Executes core business logic for mono behaviour.
public class PartyManager : MonoBehaviour
{
    // Executes core business logic for instance.
    public static PartyManager Instance { get; private set; }

    // Executes core business logic for is entering dungeon.
    public static bool IsEnteringDungeon { get; private set; }

    private PartyLobby _hookedParty;

    private bool _hostStartInProgress;
    private bool _dungeonEntryStarted;

    // Initializes internal component caches and dependencies for PartyManager upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        PartyInvitePopup.EnsureExists();

        PartyLobby.OnLocalPartyChanged += HandleLocalPartyChanged;
        RehookParty();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        PartyLobby.OnLocalPartyChanged -= HandleLocalPartyChanged;
        UnhookParty();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    // Executes core business logic for handle local party changed.
    private void HandleLocalPartyChanged()
    {
        RehookParty();

        var party = PartyLobby.Local;
        if (party != null && !party.IsLocalHost)
        {
            OpenPartyPanelForMember();
        }
    }

    // Executes core business logic for rehook party.
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
            _hostStartInProgress = false;
            _dungeonEntryStarted = false;
        }
    }

    // Executes core business logic for unhook party.
    private void UnhookParty()
    {
        if (_hookedParty != null)
        {
            _hookedParty.OnDungeonStartRequested -= HandleDungeonStartRequested;
            _hookedParty.OnPartyStateChanged -= HandlePartyState;
            _hookedParty = null;
        }
    }


    // Executes core business logic for open party panel for member.
    private void OpenPartyPanelForMember()
    {
        GameObject panelGo = null;
        if (PartyPanel.Instance != null) panelGo = PartyPanel.Instance.gameObject;
        else if (UIManager.Instance != null) panelGo = UIManager.Instance.dungeonPanel;
        if (panelGo == null)
        {
            Debug.LogWarning("[PartyManager] Cannot open party panel for member — no panel reference (UIManager.dungeonPanel unset?).");
            return;
        }
        if (panelGo.activeInHierarchy) return;

        var party = PartyLobby.Local;
        int configId = party != null ? party.DungeonConfigId : 1;
        string scene = party != null ? party.DungeonSceneName.Value : string.Empty;
        string name = party != null ? party.DungeonName.Value : "Dungeon";

        if (UIManager.Instance != null)
            UIManager.Instance.ShowPanel(panelGo);
        else
            panelGo.SetActive(true);

        var panel = panelGo.GetComponent<PartyPanel>();
        if (panel != null)
            panel.OpenForDungeon(configId, string.IsNullOrEmpty(scene) ? "HollowCryptDungeon" : scene, 0, string.IsNullOrEmpty(name) ? "Dungeon" : name);
    }


    // Executes core business logic for handle dungeon start requested.
    private void HandleDungeonStartRequested(int configId, string sceneName)
    {
        var party = PartyLobby.Local;
        if (party == null || !party.IsLocalHost) return;
        if (_hostStartInProgress) return;
        _hostStartInProgress = true;

        if (DungeonManager.Instance == null)
        {
            Debug.LogError("[PartyManager] DungeonManager missing — cannot start party dungeon.");
            _hostStartInProgress = false;
            return;
        }

        string dungeonName = party.DungeonName.Value;
        int cost = 0;

        DungeonManager.Instance.CreatePartySession(
            configId, sceneName, cost, dungeonName, BuildPartyMemberIds(party),
            sessionId =>
            {
                if (sessionId <= 0)
                {
                    Debug.LogWarning("[PartyManager] CreatePartySession failed — reverting party to Lobby.");
                    if (party != null && party.HasStateAuthority)
                    {
                        party.RevertToLobby();
                    }
                    _hostStartInProgress = false;
                    return;
                }
                party.HostPublishDungeonSession(sessionId);
                _hostStartInProgress = false;
            });
    }

    // Executes core business logic for handle party state.
    private void HandlePartyState(PartyLobby.PartyState state)
    {
        Debug.Log($"[PartyEntry] HandlePartyState({state}) fired. entryStarted={_dungeonEntryStarted} " +
                  $"isHost={(PartyLobby.Local != null ? PartyLobby.Local.IsLocalHost.ToString() : "no-local")}");
        if (state != PartyLobby.PartyState.InDungeon || _dungeonEntryStarted) return;

        var party = PartyLobby.Local;
        if (party == null)
        {
            Debug.LogWarning("[PartyEntry] InDungeon fired but PartyLobby.Local is null — cannot snapshot yet.");
            return;
        }

        _dungeonEntryStarted = true;
        IsEnteringDungeon = true;
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(EnterDungeonRoutine(
            party.DungeonConfigId,
            party.DungeonSceneName.Value,
            party.DungeonName.Value,
            party.DungeonSessionId,
            party.HostProfileId,
            party.IsLocalHost));
    }

    // Process the supplied values: normalizes or validates the text before returning the derived result.
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

        string returnMap = string.IsNullOrWhiteSpace(WorldState.CurrentMapName) ? "ElfForest" : WorldState.CurrentMapName;
        Vector3 returnPos = WorldState.LastPosition;

        yield return LoadingScreen.Show("Entering dungeon...");

        string dungeonRoom = $"DUNGEON_{hostProfileId}";

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
            yield return new WaitForSeconds(3.0f);
        }

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

    // Executes core business logic for abort entry.
    private static IEnumerator AbortEntry(string message)
    {
        yield return LoadingScreen.Hide();

        if (MainQuestPanelRuntime.Instance != null)
            MainQuestPanelRuntime.Instance.ShowPaperPopup(message, UIPaperPopupView.PaperPopupKind.None);
    }

    // Executes core business logic for destroy non networked players.
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

    // Executes core business logic for build party member ids.
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
