using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using Fusion;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Runtime State")]
    public int CurrentSessionId { get; private set; } = 0;
    public int CurrentDungeonConfigId { get; private set; } = 0;
    public int CurrentDungeonCost { get; private set; } = 0;
    public string CurrentDungeonName { get; private set; } = string.Empty;
    public int EnemiesKilledCount { get; private set; } = 0;
    public bool IsInDungeon { get; private set; } = false;

    // Added to support UI Progress
    public int TotalNormalEnemies => EnemiesKilledCount + _normalEnemies.Count;
    public int BossCount => _bossEnemies.Count;
    public Dictionary<string, (int killed, int total)> EnemyProgress { get; private set; } = new();

    // Saved position in world map to return to
    public string PreviousMapSceneName { get; private set; } = "AbandonedCastle";
    public Vector3 PreviousPlayerPosition { get; private set; } = Vector3.zero;
    public bool HasPreviousPlayerPosition { get; private set; } = false;

    // ── Per-run enemy tracking (normal monsters and boss are tracked separately) ──
    private readonly List<EnemyEntity> _normalEnemies = new();
    private readonly List<EnemyEntity> _bossEnemies   = new();
    private bool bossKilled = false;
    private Vector3 _bossDeathPosition = Vector3.zero;

    // ── Saved state for RestartDungeon ──
    private List<string> _currentPartyMembers = new();
    private string _currentDungeonSceneName = string.Empty;

    private enum DungeonPhase { Normal, BossSpawning, Boss, Complete }
    private DungeonPhase _currentPhase = DungeonPhase.Normal;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private GameObject FindPlayerInstance()
    {
        GameObject found = null;

        // Prefer the local networked avatar in multiplayer — its own PlayerMovement is
        // on the NetworkPlayer root, whereas FindFirstObjectByType<PlayerMovement> can
        // match the class VISUAL child (which briefly carries a PlayerMovement before
        // CharacterFactory strips it). A child is not a scene root, so returning it
        // makes MoveGameObjectToScene throw "Gameobject is not a root in a scene".
        if (NetworkPlayer.Local != null)
            found = NetworkPlayer.Local.gameObject;

        if (found == null)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) found = pm.gameObject;
        }

        if (found == null)
        {
            var pwi = FindFirstObjectByType<PlayerWorldInteractor>();
            if (pwi != null) found = pwi.gameObject;
        }

        found ??= GameObject.FindWithTag("Player") ??
                  GameObject.Find("Knight") ??
                  GameObject.Find("Mage") ??
                  GameObject.Find("Archer") ??
                  GameObject.Find("Knight(Clone)") ??
                  GameObject.Find("Mage(Clone)") ??
                  GameObject.Find("Archer(Clone)");

        // Always return the scene ROOT: SceneManager.MoveGameObjectToScene rejects any
        // non-root object. Fusion spawns network avatars as scene roots, so transform.root
        // resolves to the avatar root even if we matched a child component above.
        return found != null ? found.transform.root.gameObject : null;
    }

    /// <summary>
    /// Move a player into a scene without letting a failure kill the calling coroutine.
    /// MoveGameObjectToScene throws if the object is not a scene root; guarding it here
    /// means a bad move logs an error but the dungeon transition still completes (avoids
    /// the half-loaded "black screen" hang). Returns true on success.
    /// </summary>
    private static bool SafeMoveToScene(GameObject go, Scene scene)
    {
        if (go == null || !scene.IsValid() || !scene.isLoaded) return false;
        if (go.scene == scene) return true; // already there
        try
        {
            // MoveGameObjectToScene requires a scene root; force it defensively.
            var root = go.transform.root.gameObject;
            SceneManager.MoveGameObjectToScene(root, scene);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DungeonManager] MoveGameObjectToScene('{go.name}' → '{scene.name}') failed: {ex.Message}");
            return false;
        }
    }
    public void StartDungeon(int configId, string dungeonSceneName, int cost, string dungeonName, List<string> partyMembers = null)
    {
        CurrentDungeonConfigId = configId;
        CurrentDungeonCost = cost;
        CurrentDungeonName = dungeonName;
        _currentPartyMembers = partyMembers ?? new List<string>();
        _currentDungeonSceneName = dungeonSceneName;

        EnemiesKilledCount = 0;
        bossKilled = false;
        _currentPhase = DungeonPhase.Normal;
        _normalEnemies.Clear();
        _bossEnemies.Clear();
        _bossDeathPosition = Vector3.zero;

        // Save current map state to return later
        PreviousMapSceneName = WorldState.CurrentMapName;
        
        // Find player position in the scene
        var player = FindPlayerInstance();
        if (player != null && player.transform.position != Vector3.zero)
        {
            PreviousPlayerPosition = player.transform.position;
            HasPreviousPlayerPosition = true;
        }
        else
        {
            // Fallback to the globally synced position if player is disabled or not found
            PreviousPlayerPosition = WorldState.LastPosition;
            HasPreviousPlayerPosition = true; // Even if it's 0,0,0 it's explicitly saved
        }

        // Call Enter API
        DungeonApi.Instance.Enter(configId, partyMembers ?? new List<string>(),
            onSuccess: response =>
            {
                if (response != null)
                {
                    CurrentSessionId = response.DungeonSessionId;
                    IsInDungeon = true;
                    Debug.Log($"[DungeonManager] Session created: {CurrentSessionId}");
                    
                    // Transition to target scene
                    StartCoroutine(TransitionToDungeon(dungeonSceneName));
                }
                else
                {
                    Debug.LogWarning("[DungeonManager] Enter API succeeded but no session data returned. Proceeding anyway for testing.");
                    CurrentSessionId = -1; // Dummy session ID
                    IsInDungeon = true;
                    StartCoroutine(TransitionToDungeon(dungeonSceneName));
                }
            },
            onError: error =>
            {
                Debug.LogWarning($"[DungeonManager] Enter API failed: {error.Message}. Proceeding to dungeon anyway for testing.");
                CurrentSessionId = -1; // Dummy session ID
                IsInDungeon = true;
                StartCoroutine(TransitionToDungeon(dungeonSceneName));
            }
        );
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTY DUNGEON ENTRY (multiplayer)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// HOST path: create the backend dungeon session (Enter API) ONCE, then hand the
    /// resulting session id back so the host can publish it to the party. The scene
    /// transition itself is driven separately (after every member has migrated) via
    /// <see cref="EnterDungeonScene"/>. Falls back to a dummy session id on API error
    /// (matching the existing solo behaviour).
    /// </summary>
public void CreatePartySession(int configId, string dungeonSceneName, int cost, string dungeonName,
                                  List<string> partyMembers, Action<int> onReady)
    {
        CurrentDungeonConfigId = configId;
        CurrentDungeonCost = cost;
        CurrentDungeonName = dungeonName;
        _currentPartyMembers = partyMembers ?? new List<string>();
        _currentDungeonSceneName = dungeonSceneName;

        DungeonApi.Instance.Enter(configId, _currentPartyMembers,
            onSuccess: response =>
            {
                CurrentSessionId = response != null ? response.DungeonSessionId : 0;
                if (CurrentSessionId <= 0)
                {
                    Debug.LogWarning("[DungeonManager] Party Enter API returned no session id. Aborting dungeon entry.");
                    WorldRuntimeEvents.RaiseMessage("Cannot start dungeon: backend returned no session.");
                    onReady?.Invoke(0);
                    return;
                }
                onReady?.Invoke(CurrentSessionId);
            },
            onError: error =>
            {
                Debug.LogWarning($"[DungeonManager] Party Enter API failed: {error.Message}. Aborting dungeon entry.");
                CurrentSessionId = 0;
                WorldRuntimeEvents.RaiseMessage($"Cannot start dungeon: {error.Message}");
                onReady?.Invoke(0);
            }
        );
    }

    /// <summary>True if the local player is the host of the current party dungeon.</summary>
    public bool IsPartyHost { get; private set; }

    /// <summary>
    /// EVERY client (host + members): perform the actual scene transition into the
    /// dungeon using an already-established session id (from the host). Does NOT call
    /// the Enter API again — members reuse the host's session. Reuses the existing
    /// <see cref="TransitionToDungeon"/> pipeline so camera/scene handling is identical
    /// to the solo flow. The networked avatar (spawned by PhotonManager on migration)
    /// is picked up by FindPlayerInstance just like a local player.
    /// </summary>
    public void EnterDungeonScene(int configId, string dungeonSceneName, int cost, string dungeonName, int sessionId,
                                  bool hasReturnPoint = false, string returnMapName = null, Vector3 returnPosition = default, bool isHost = false)
    {
        if (sessionId <= 0)
        {
            Debug.LogWarning("[DungeonManager] EnterDungeonScene aborted: invalid session id.");
            WorldRuntimeEvents.RaiseMessage("Cannot enter dungeon: backend session missing.");
            return;
        }

        IsPartyHost = isHost;
        CurrentDungeonConfigId = configId;
        CurrentDungeonCost = cost;
        CurrentDungeonName = dungeonName?.Trim('\0');
        
        // Clean trailing nulls in case it came from a Fusion NetworkString
        _currentDungeonSceneName = dungeonSceneName?.Trim('\0');
        
        CurrentSessionId = sessionId;
        IsInDungeon = true;

        EnemiesKilledCount = 0;
        bossKilled = false;
        _currentPhase = DungeonPhase.Normal;
        _normalEnemies.Clear();
        _bossEnemies.Clear();
        _bossDeathPosition = Vector3.zero;

        // Return point: the party path migrates the Photon runner FIRST, which destroys
        // the world avatar and spawns a fresh networked one at a different position — so
        // reading the (post-migration) avatar's transform here would save the wrong
        // "previous" position and exit the dungeon to the wrong spot. The caller
        // (PartyManager) captures the true world position BEFORE migrating and passes it
        // in. Solo entry (StartDungeon) captures it there and does not use this method.
        if (hasReturnPoint)
        {
            PreviousMapSceneName = string.IsNullOrEmpty(returnMapName) ? WorldState.CurrentMapName : returnMapName;
            PreviousPlayerPosition = returnPosition;
            HasPreviousPlayerPosition = true;
        }
        else
        {
            PreviousMapSceneName = WorldState.CurrentMapName;
            var player = FindPlayerInstance();
            if (player != null && player.transform.position != Vector3.zero)
            {
                PreviousPlayerPosition = player.transform.position;
                HasPreviousPlayerPosition = true;
            }
            else
            {
                PreviousPlayerPosition = WorldState.LastPosition;
                HasPreviousPlayerPosition = true;
            }
        }

        StartCoroutine(TransitionToDungeon(dungeonSceneName));
    }

    private IEnumerator TransitionToDungeon(string dungeonSceneName)
    {
        // Che màn hình trước khi unload map: đoạn dưới còn chờ tới 5s cho avatar network
        // xuất hiện, không có loading thì người chơi ngồi nhìn scene trống suốt lúc đó.
        yield return LoadingScreen.Show("Entering dungeon...");

        // Find player first before unloading
        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainScene = SceneManager.GetSceneByName("Main");
            if (SafeMoveToScene(player, mainScene))
                Debug.Log("[DungeonManager] Moved player to Main scene defensively.");
        }

        // Unload any active map scenes defensively (excluding "Main" and the target dungeon scene)
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "Main" && s.name != dungeonSceneName && s.name != LoadingScreen.SceneName && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // Set LastPosition to zero so player spawner falls back to scene spawnPoint
        WorldState.LastPosition = Vector3.zero;
        WorldState.CurrentMapName = _currentDungeonSceneName;

        // Load dungeon scene additively
        yield return SceneManager.LoadSceneAsync(_currentDungeonSceneName, LoadSceneMode.Additive);

        // The local (networked) avatar may not exist yet on a client that just migrated
        // — if it is still null here we skip the teleport and the player is left at the
        // spawn position NetworkPlayer chose (world position + fan-out offset), so the
        // two players end up in different spots. Wait briefly for it so EVERY client
        // teleports its own avatar to the shared PlayerSpawn.
        if (player == null)
        {
            float waitAvatar = 5f;
            while (waitAvatar > 0f && (player = FindPlayerInstance()) == null)
            {
                waitAvatar -= Time.deltaTime;
                yield return null;
            }
        }

        // Move player into the dungeon scene
        if (player != null)
        {
            var dungeonScene = SceneManager.GetSceneByName(_currentDungeonSceneName);
            if (SafeMoveToScene(player, dungeonScene))
                Debug.Log($"[DungeonManager] Moved player into dungeon scene: {_currentDungeonSceneName}");
        }
        else
        {
            Debug.LogWarning("[DungeonManager] Local avatar still null after wait — teleport to PlayerSpawn will be skipped.");
        }

        // Teleport player to the PlayerSpawn position
        GameObject targetSpawnPoint = GameObject.Find("PlayerSpawn") ?? GameObject.Find("SceneTransitionGoblinMine");
        if (targetSpawnPoint == null)
        {
            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in allTransforms)
            {
                if (t != null && (t.gameObject.name == "PlayerSpawn" || t.gameObject.name == "SceneTransitionGoblinMine") && t.gameObject.scene.name == dungeonSceneName)
                {
                    targetSpawnPoint = t.gameObject;
                    break;
                }
            }
        }

        if (targetSpawnPoint != null)
        {
            Vector3 spawnPos = targetSpawnPoint.transform.position;
            Debug.Log($"[DungeonManager] Found {targetSpawnPoint.name} at {spawnPos}. Teleporting player.");
            if (player != null)
            {
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.position = spawnPos;
                }
                
                var nt = player.GetComponent<Fusion.NetworkTransform>();
                if (nt != null)
                    nt.Teleport(spawnPos);
                else
                    player.transform.position = spawnPos;

                WorldState.LastPosition = spawnPos;
            }
        }
        else
        {
            Debug.LogWarning("[DungeonManager] PlayerSpawn point not found in scene!");
        }

        // Bind camera to player in target scene
        if (player != null)
        {
            BindCameraToPlayer(player, dungeonSceneName);
        }

        // Keep Main active
        var mainSceneObj = SceneManager.GetSceneByName("Main");
        if (mainSceneObj.IsValid())
        {
            SceneManager.SetActiveScene(mainSceneObj);
        }

        PlayerHUDController.Instance?.ToggleDungeonMode(true);
        Debug.Log($"[DungeonManager] Entered dungeon scene: {dungeonSceneName}");

        yield return LoadingScreen.Hide();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsInDungeon || scene.name != WorldState.CurrentMapName)
            return;

        Debug.Log($"[DungeonManager] Dungeon scene loaded: {scene.name}. Starting spawn + registration...");

        // Try to use DungeonSpawner for data-driven spawning.
        // Falls back to scanning existing scene enemies if no spawner is present.
        StartCoroutine(SpawnAndRegisterEnemies(scene.name));
    }

    /// <summary>
    /// Primary enemy registration coroutine.
    /// Looks for a DungeonSpawner in the dungeon scene and drives the full
    /// two-phase spawn pipeline (API fetch → allocate → instantiate).
    /// If no DungeonSpawner is found, falls back to registering pre-placed enemies.
    /// </summary>
    private IEnumerator SpawnAndRegisterEnemies(string mapName)
    {
        // Wait one frame so Awake/Start have all completed in the loaded scene
        yield return null;

        var photon = PhotonManager.Instance;
        bool online = photon != null && photon.IsDungeonSession;

        // In multiplayer, the Party Host MUST be the Master Client (IsHost) to spawn monsters.
        // Fusion sometimes takes a few ticks to assert IsSharedModeMasterClient after migration.
        // If we spawn before this happens, Runner.Spawn is skipped and no monsters appear.
        if (online && IsPartyHost)
        {
            float waitMaster = 5f;
            while (waitMaster > 0f && !photon.IsHost)
            {
                waitMaster -= Time.deltaTime;
                yield return null;
            }
            if (!photon.IsHost)
            {
                Debug.LogWarning("[DungeonManager] Timed out waiting to become Master Client! Spawns may fail.");
            }
        }

        var spawner = FindFirstObjectByType<DungeonSpawner>();

        if (spawner != null)
        {
            // ── DungeonSpawner path: data-driven, backend-driven spawning ───────
            Debug.Log("[DungeonManager] DungeonSpawner found — running data-driven spawn pipeline.");

            bool spawnDone = false;
            List<EnemyEntity> spawnedEnemies = null;

            spawner.SpawnMonstersForDungeon(
                CurrentDungeonConfigId,
                mapName,
                enemies =>
                {
                    spawnedEnemies = enemies;
                    spawnDone = true;
                }
            );

            yield return new WaitUntil(() => spawnDone);

            if (spawnedEnemies != null)
            {
                photon = PhotonManager.Instance;
                bool isProxy = photon != null && photon.IsDungeonSession && !photon.IsHost;
                
                if (!isProxy)
                {
                    _normalEnemies.Clear();
                    EnemyProgress.Clear();
                    foreach (var enemy in spawnedEnemies)
                    {
                        RegisterNetworkedEnemy(enemy);
                    }
                    Debug.Log($"[DungeonManager] Registered {_normalEnemies.Count} normal enemies.");
                }
                else
                {
                    Debug.Log("[DungeonManager] Proxy client waiting for NetworkEnemy spawns.");
                }
            }
        }
        else
        {
            // ── Fallback path: scan scene for manually-placed EnemyEntity objects ─
            Debug.LogWarning("[DungeonManager] No DungeonSpawner found in scene. " +
                             "Falling back to scanning for pre-placed EnemyEntity objects. " +
                             "Add a DungeonSpawner component to the dungeon scene for data-driven spawning.");
            yield return new WaitForSeconds(0.5f);

            var enemies = FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Debug.Log($"[DungeonManager] Fallback: found {enemies.Length} pre-placed enemies.");

            _normalEnemies.Clear();
            EnemyProgress.Clear();
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                _normalEnemies.Add(enemy);
                enemy.OnDeath -= HandleNormalEnemyDeath;
                enemy.OnDeath += HandleNormalEnemyDeath;

                string n = GetCleanEnemyName(enemy);
                if (!EnemyProgress.ContainsKey(n)) EnemyProgress[n] = (0, 0);
                var p = EnemyProgress[n];
                p.total++;
                EnemyProgress[n] = p;
            }
        }
    }

    public void RegisterNetworkedEnemy(EnemyEntity enemy)
    {
        if (enemy == null) return;
        
        bool isBoss = enemy.gameObject.name.EndsWith("_Boss");

        if (isBoss)
        {
            if (!_bossEnemies.Contains(enemy))
            {
                _bossEnemies.Add(enemy);
                enemy.OnDeath -= HandleBossEnemyDeath;
                enemy.OnDeath += HandleBossEnemyDeath;
            }
        }
        else
        {
            if (!_normalEnemies.Contains(enemy))
            {
                _normalEnemies.Add(enemy);
                enemy.OnDeath -= HandleNormalEnemyDeath;
                enemy.OnDeath += HandleNormalEnemyDeath;

                string n = GetCleanEnemyName(enemy);
                if (!EnemyProgress.ContainsKey(n)) EnemyProgress[n] = (0, 0);
                var p = EnemyProgress[n];
                p.total++;
                EnemyProgress[n] = p;
                Debug.Log($"[DungeonManager] Registered networked enemy: {n} (Total: {p.total})");
            }
        }
    }

    private string GetCleanEnemyName(EnemyEntity enemy)
    {
        if (enemy == null) return "Unknown";
        string cleanName = enemy.gameObject.name.Replace("(Clone)", "").Trim();
        int spaceIndex = cleanName.IndexOf(" (");
        if (spaceIndex > 0) cleanName = cleanName.Substring(0, spaceIndex);
        
        int lastUnderscore = cleanName.LastIndexOf('_');
        if (lastUnderscore > 0 && lastUnderscore < cleanName.Length - 1)
        {
            string suffix = cleanName.Substring(lastUnderscore + 1);
            if (int.TryParse(suffix, out _))
            {
                cleanName = cleanName.Substring(0, lastUnderscore);
            }
        }
        
        return cleanName;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ENEMY DEATH HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles death of a normal (non-boss) enemy.
    /// Reports partial progress to the backend.
    /// When the LAST normal enemy dies → initiates the boss spawn sequence.
    /// </summary>
    private void HandleNormalEnemyDeath(object sender, EventArgs e)
    {
        if (sender is not EnemyEntity enemy) return;

        enemy.OnDeath -= HandleNormalEnemyDeath;
        _normalEnemies.Remove(enemy);
        EnemiesKilledCount++;

        string n = GetCleanEnemyName(enemy);
        if (EnemyProgress.ContainsKey(n))
        {
            var p = EnemyProgress[n];
            p.killed++;
            EnemyProgress[n] = p;
        }

        int remaining  = _normalEnemies.Count;
        int total      = EnemiesKilledCount + remaining;
        // Progress stays ≤ 49 % while normals are alive; hits 50 % when all are dead
        int percentage = remaining == 0 ? 50
                       : Mathf.Min(49, (EnemiesKilledCount * 50) / Mathf.Max(1, total));

        Debug.Log($"[DungeonManager] Normal enemy killed. Remaining: {remaining}. Progress: {percentage}%");

        // Fire-and-forget progress update (only host should call backend API)
        if (PhotonManager.Instance?.IsHost == true)
        {
            DungeonApi.Instance.UpdateProgress(
                CurrentSessionId,
                new UpdateDungeonProgressRequest
                {
                    MonstersKilled       = EnemiesKilledCount,
                    BossKilled           = false,
                    CompletionPercentage = percentage
                },
                _ => { },
                err => Debug.LogWarning($"[DungeonManager] UpdateProgress (normal) failed: {err.Message}")
            );
        }

        if (remaining == 0 && _currentPhase == DungeonPhase.Normal)
            StartCoroutine(TriggerBossSequence());
    }

    /// <summary>
    /// Screen shake warning → 1-second pause → spawns boss at BossSpawn point.
    /// </summary>
    private IEnumerator TriggerBossSequence()
    {
        _currentPhase = DungeonPhase.BossSpawning;
        Debug.Log("[DungeonManager] All normals defeated. Starting boss sequence (shake → spawn).");

        if (PhotonManager.Instance?.IsHost == true && NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.RPC_BossSpawning();
        }

        // Screen shake to signal the incoming boss
        DungeonScreenShake.Shake(duration: 0.9f, magnitude: 0.28f);
        yield return new WaitForSeconds(1.2f);

        // Find DungeonSpawner and let it spawn the Boss (which was saved from the API call)
        if (DungeonSpawner.Instance == null)
        {
            Debug.LogWarning("[DungeonManager] DungeonSpawner not found. Skipping boss and completing dungeon.");
            yield return StartCoroutine(BossDeathSequence(GetFallbackChestPosition()));
            yield break;
        }

        EnemyEntity boss = DungeonSpawner.Instance.SpawnBoss();
        if (boss != null)
        {
            RegisterNetworkedEnemy(boss);
        }
        
        if (boss == null)
        {
            Debug.LogWarning("[DungeonManager] DungeonSpawner.SpawnBoss returned null. Completing dungeon without boss.");
            yield return StartCoroutine(BossDeathSequence(GetFallbackChestPosition()));
            yield break;
        }

        _currentPhase = DungeonPhase.Boss;
        Debug.Log($"[DungeonManager] Boss '{boss.name}' spawned. Phase → Boss.");
    }

    /// <summary>
    /// Handles boss death. Captures death position, then starts the completion sequence.
    /// </summary>
    private void HandleBossEnemyDeath(object sender, EventArgs e)
    {
        if (sender is not EnemyEntity boss) return;

        boss.OnDeath -= HandleBossEnemyDeath;
        _bossEnemies.Remove(boss);
        bossKilled           = true;
        EnemiesKilledCount++;
        _bossDeathPosition   = boss.transform.position;
        _currentPhase        = DungeonPhase.Complete;

        if (PhotonManager.Instance?.IsHost == true && NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.RPC_BossDied(_bossDeathPosition);
        }

        Debug.Log($"[DungeonManager] Boss defeated at {_bossDeathPosition}. Starting completion sequence.");
        StartCoroutine(BossDeathSequence(_bossDeathPosition));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CLIENT EVENT RECEIVERS
    // ═══════════════════════════════════════════════════════════════════════════

    public void ClientReceiveBossSpawning()
    {
        StartCoroutine(ClientBossSequence());
    }

    private IEnumerator ClientBossSequence()
    {
        _currentPhase = DungeonPhase.BossSpawning;
        Debug.Log("[DungeonManager] Client received boss spawning event. Shaking screen...");
        DungeonScreenShake.Shake(duration: 0.9f, magnitude: 0.28f);
        yield return new WaitForSeconds(1.2f);
        _currentPhase = DungeonPhase.Boss;
    }

    public void ClientReceiveBossDeath(Vector3 chestPosition)
    {
        _currentPhase = DungeonPhase.Complete;
        Debug.Log($"[DungeonManager] Client received boss death event at {chestPosition}. Starting completion sequence.");
        StartCoroutine(BossDeathSequence(chestPosition));
    }

    /// <summary>
    /// Reports 100% completion to the backend, waits 1.5 seconds for the boss death
    /// animation to finish, then spawns the reward chest with a drop-in animation.
    /// </summary>
    private IEnumerator BossDeathSequence(Vector3 chestPosition)
    {
        bool updateDone = false;
        bool completeDone = false;

        // Report final progress FIRST and wait for it (only host should call backend API)
        if (PhotonManager.Instance?.IsHost == true)
        {
            DungeonApi.Instance.UpdateProgress(
                CurrentSessionId,
                new UpdateDungeonProgressRequest
                {
                    MonstersKilled       = EnemiesKilledCount,
                    BossKilled           = bossKilled,
                    CompletionPercentage = 100
                },
                _ => { updateDone = true; },
                err => 
                { 
                    Debug.LogWarning($"[DungeonManager] Final UpdateProgress failed: {err.Message}");
                    updateDone = true; 
                }
            );

            // Wait for the backend to acknowledge the boss kill
            yield return new WaitUntil(() => updateDone);

            // NOW mark session complete on backend
            DungeonApi.Instance.Complete(
                CurrentSessionId,
                response =>
                {
                    Debug.Log("[DungeonManager] Session marked complete on backend.");
                    completeDone = true;
                },
                error =>
                {
                    Debug.LogWarning($"[DungeonManager] Complete API failed: {error.Message}. Spawning chest anyway.");
                    completeDone = true;
                }
            );

            yield return new WaitUntil(() => completeDone);
        }
        else
        {
            // Non-host: mark done immediately
            updateDone = true;
            completeDone = true;
        }

        // Wait for boss death animation
        yield return new WaitForSeconds(1.5f);

        // Spawn the reward chest with drop-in animation
        SpawnFinalChestAtPosition(chestPosition);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CHEST SPAWNING
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("Dungeon Rewards")]
    [Tooltip("Kéo Prefab Rương của bạn vào đây (vd: DarkChest)")]
    public GameObject rewardChestPrefab;

    private void SpawnFinalChestAtPosition(Vector3 targetPosition)
    {
        GameObject chestGO = null;

        // 1. Instantiate from assigned prefab
        if (rewardChestPrefab != null)
        {
            chestGO = Instantiate(rewardChestPrefab, targetPosition + Vector3.up * 6f, Quaternion.identity);
            chestGO.name = "DungeonChest";
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chestGO, UnityEngine.SceneManagement.SceneManager.GetSceneByName(WorldState.CurrentMapName));
            
            // Ensure components are present and active
            var chestScript = chestGO.GetComponent<DungeonChest>();
            if (chestScript == null) chestScript = chestGO.AddComponent<DungeonChest>();
            chestScript.enabled = true; // Force enable in case it was disabled in prefab

            Debug.Log("[DungeonManager] Spawned chest from assigned prefab with drop animation.");
        }
        else
        {
            // 2. Instantiate from Resources (legacy paths)
            var prefab = Resources.Load<GameObject>("Prefabs/Chest")
                      ?? Resources.Load<GameObject>("Chest")
                      ?? Resources.Load<GameObject>("DarkChest")
                      ?? Resources.Load<GameObject>("PixelWorld/Prefabs/Objects/BoxesChests/Chest_1");

            if (prefab != null)
            {
                chestGO = Instantiate(prefab, targetPosition + Vector3.up * 6f, Quaternion.identity);
                chestGO.name = "DungeonChest";
                var chestScript = chestGO.GetComponent<DungeonChest>();
                if (chestScript == null) chestScript = chestGO.AddComponent<DungeonChest>();
                chestScript.enabled = true;
                Debug.Log("[DungeonManager] Spawned chest from Resources prefab.");
            }
        }

        // 3. Hard fallback: 2D Sprite (visible in 2D view)
        if (chestGO == null)
        {
            chestGO = new GameObject("DungeonChest");
            chestGO.transform.position = targetPosition + Vector3.up * 6f;
            
            var sr = chestGO.AddComponent<SpriteRenderer>();
            Sprite defaultSprite = Resources.Load<Sprite>("UI/Skin/UISprite.psd") ?? Resources.Load<Sprite>("Background");
            sr.sprite = defaultSprite;
            sr.color = Color.yellow; 
            
            var col = chestGO.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 1.5f);

            chestGO.AddComponent<DungeonChest>();
            Debug.LogWarning("[DungeonManager] Chest prefab not found — created a fallback yellow 2D Sprite chest.");
        }

        StartCoroutine(ChestDropAnimation(chestGO, targetPosition));
    }

    /// <summary>
    /// Animates the chest falling from 6 units above the target to the target position.
    /// Uses ease-out cubic for a natural bouncy landing feel.
    /// </summary>
    private IEnumerator ChestDropAnimation(GameObject chest, Vector3 targetPosition)
    {
        if (chest == null) yield break;

        Vector3 startPos = chest.transform.position; // already offset upward by caller
        float elapsed    = 0f;
        const float duration = 0.55f;

        while (elapsed < duration)
        {
            if (chest == null) yield break;
            float t     = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            chest.transform.position = Vector3.Lerp(startPos, targetPosition, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (chest != null)
            chest.transform.position = targetPosition;

        Debug.Log($"[DungeonManager] Reward chest landed at {targetPosition}.");
    }

    private Vector3 GetFallbackChestPosition()
    {
        var player = FindPlayerInstance();
        return player != null ? player.transform.position + Vector3.right * 2f : Vector3.zero;
    }

    /// <summary>Kept for external callers — logic now handled by the new two-phase death system.</summary>
    [System.Obsolete("Death events are now handled automatically by HandleNormalEnemyDeath and HandleBossEnemyDeath.")]
    public void UpdateMonsterKill(bool isBoss)
    {
        Debug.LogWarning("[DungeonManager] UpdateMonsterKill is deprecated and does nothing. " +
                         "Death events are handled automatically.");
    }

    public void ReturnToWorldMap()
    {
        StartCoroutine(TransitionToWorld());
    }

    private IEnumerator TransitionToWorld()
    {
        // Che cả đoạn migrate Photon lẫn scene swap — migrate là await mạng, thời gian không
        // đoán được, để lộ scene dungeon đã unload thì rất xấu.
        yield return LoadingScreen.Show("Returning to world...");

        // If we entered the dungeon as a networked party, leave the dungeon Photon room
        // and return to the shared social lobby FIRST. This tears down the dungeon runner
        // so our avatar despawns for the other members (and theirs for us) — otherwise
        // both players keep seeing each other in the world because they're still in the
        // same dungeon room. Done before the scene swap so the networked avatar is gone
        // before PlayerSpawner puts a local one back.
        var photon = PhotonManager.Instance;
        if (photon != null && photon.IsDungeonSession)
        {
            Debug.Log("[DungeonManager] Exiting dungeon room → migrating back to social lobby.");
            var migrate = photon.MigrateToSocialLobbyAsync();
            while (!migrate.IsCompleted) yield return null;
        }

        // Find player first before unloading
        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainSceneObj = SceneManager.GetSceneByName("Main");
            if (SafeMoveToScene(player, mainSceneObj))
                Debug.Log("[DungeonManager] Moved player to Main scene defensively.");
        }

        // Unload any active map scenes defensively (excluding "Main" and the target world scene)
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "Main" && s.name != PreviousMapSceneName && s.name != LoadingScreen.SceneName && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // Restore position and current map
        Vector3 returnPos = HasPreviousPlayerPosition ? PreviousPlayerPosition : WorldState.LastPosition;
        if (!HasPreviousPlayerPosition && returnPos == Vector3.zero) returnPos = new Vector3(11.9f, 17.8f, 0f); // Final hard fallback
        WorldState.LastPosition = returnPos;
        WorldState.CurrentMapName = PreviousMapSceneName;

        // Load previous map
        yield return SceneManager.LoadSceneAsync(PreviousMapSceneName, LoadSceneMode.Additive);

        // Move player into the world scene and set physical position
        if (player != null)
        {
            var worldScene = SceneManager.GetSceneByName(PreviousMapSceneName);
            if (SafeMoveToScene(player, worldScene))
            {
                player.transform.position = returnPos;
                Debug.Log($"[DungeonManager] Moved player into world scene: {PreviousMapSceneName} at {returnPos}");

                // Save position to backend so logout doesn't get stuck in dungeon
                MysticJourney.API.Endpoints.WorldApi.Instance?.UpdatePosition(PreviousMapSceneName, returnPos, null, null);
            }
        }
        else
        {
            player = FindPlayerInstance();
        }

        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = WorldState.LastPosition;
            }
            player.transform.position = WorldState.LastPosition;
            BindCameraToPlayer(player, PreviousMapSceneName);
        }

        // Keep Main active
        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid())
        {
            SceneManager.SetActiveScene(mainScene);
        }

        PlayerHUDController.Instance?.ToggleDungeonMode(false);
        IsInDungeon = false;
        Debug.Log($"[DungeonManager] Returned to map: {PreviousMapSceneName} at {WorldState.LastPosition}");

        yield return LoadingScreen.Hide();
    }

    public void RestartDungeon()
    {
        Debug.Log("[DungeonManager] Restarting Dungeon...");
        EnemiesKilledCount = 0;
        bossKilled = false;
        _currentPhase = DungeonPhase.Normal;
        _normalEnemies.Clear();
        _bossEnemies.Clear();
        _bossDeathPosition = Vector3.zero;

        // Note: PreviousMapSceneName and PreviousPlayerPosition are preserved from the FIRST time they entered!

        // Only host should call Enter API for restart
        if (PhotonManager.Instance?.IsHost == true)
        {
            DungeonApi.Instance.Enter(CurrentDungeonConfigId, _currentPartyMembers,
                onSuccess: response =>
                {
                    if (response != null)
                    {
                        CurrentSessionId = response.DungeonSessionId;
                        IsInDungeon = true;
                        Debug.Log($"[DungeonManager] Session created for Restart: {CurrentSessionId}");
                        
                        // Close the Dungeon Complete panel since it lives in Main scene
                        var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
                        if (p != null) p.gameObject.SetActive(false);

                        string sceneToLoad = _currentDungeonSceneName;
                        StartCoroutine(TransitionToRestart(sceneToLoad));
                    }
                },
                onError: error =>
                {
                    Debug.LogWarning($"[DungeonManager] Restart API failed: {error.Message}. Proceeding to restart anyway for testing.");
                    WorldRuntimeEvents.RaiseMessage($"Cannot Restart API: {error.Message}");
                    
                    CurrentSessionId = -1;
                    IsInDungeon = true;
                    // Close the Dungeon Complete panel since it lives in Main scene
                    var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
                    if (p != null) p.gameObject.SetActive(false);

                    string sceneToLoad = _currentDungeonSceneName;
                    StartCoroutine(TransitionToRestart(sceneToLoad));
                }
            );
        }
        else
        {
            // Non-host: just restart scene locally, don't create new session
            Debug.Log("[DungeonManager] Non-host restarting dungeon scene locally.");
            IsInDungeon = true;
            var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
            if (p != null) p.gameObject.SetActive(false);
            StartCoroutine(TransitionToRestart(_currentDungeonSceneName));
        }
    }

    private IEnumerator TransitionToRestart(string dungeonSceneName)
    {
        yield return LoadingScreen.Show("Restarting dungeon...");

        // 1. Find player and move them to Main defensively so they survive the reload
        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainScene = SceneManager.GetSceneByName("Main");
            SafeMoveToScene(player, mainScene);
        }

        // 2. Unload the CURRENT dungeon scene
        var currentDungeonScene = SceneManager.GetSceneByName(dungeonSceneName);
        if (currentDungeonScene.IsValid() && currentDungeonScene.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(currentDungeonScene);
        }

        // 3. Load the dungeon scene fresh
        yield return SceneManager.LoadSceneAsync(dungeonSceneName, LoadSceneMode.Additive);

        // 4. Move player back in
        if (player != null)
        {
            var newDungeonScene = SceneManager.GetSceneByName(dungeonSceneName);
            if (SafeMoveToScene(player, newDungeonScene))
            {
                GameObject targetSpawnPoint = GameObject.Find("PlayerSpawn") ?? GameObject.Find("SceneTransitionGoblinMine");
                if (targetSpawnPoint == null)
                {
                    var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                    foreach (var t in allTransforms)
                    {
                        if (t != null && (t.gameObject.name == "PlayerSpawn" || t.gameObject.name == "SceneTransitionGoblinMine") && t.gameObject.scene.name == dungeonSceneName)
                        {
                            targetSpawnPoint = t.gameObject;
                            break;
                        }
                    }
                }

                if (targetSpawnPoint != null)
                {
                    Vector3 spawnPos = targetSpawnPoint.transform.position;
                    
                    var nt = player.GetComponent<NetworkTransform>();
                    if (nt != null)
                    {
                        nt.Teleport(spawnPos);
                    }
                    else
                    {
                        player.transform.position = spawnPos;
                    }

                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                        rb.position = spawnPos;
                    }
                    WorldState.LastPosition = spawnPos;
                }
                else
                {
                    var nt = player.GetComponent<NetworkTransform>();
                    if (nt != null)
                    {
                        nt.Teleport(Vector3.zero);
                    }
                    else
                    {
                        player.transform.position = Vector3.zero;
                    }
                    WorldState.LastPosition = Vector3.zero;
                }
                
                BindCameraToPlayer(player, dungeonSceneName);
            }
        }
        else
        {
            Debug.LogError("[DungeonManager] Restart failed to locate Player! Camera not bound.");
        }

        // Reset all NetworkPlayers for restart (restore HP, IsAlive, IsReadyToRestart)
        Vector3 finalSpawnPos = Vector3.zero;
        GameObject sp = GameObject.Find("PlayerSpawn") ?? GameObject.Find("SceneTransitionGoblinMine");
        if (sp == null)
        {
            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in allTransforms)
            {
                if (t != null && (t.gameObject.name == "PlayerSpawn" || t.gameObject.name == "SceneTransitionGoblinMine") && t.gameObject.scene.name == dungeonSceneName)
                {
                    sp = t.gameObject;
                    break;
                }
            }
        }
        if (sp != null) finalSpawnPos = sp.transform.position;

        if (NetworkPlayer.All != null)
        {
            foreach (var p in NetworkPlayer.All)
            {
                if (p != null)
                {
                    p.ResetForRestart(finalSpawnPos);
                }
            }
        }

        // Keep Main active
        var mainActiveScene = SceneManager.GetSceneByName("Main");
        if (mainActiveScene.IsValid())
        {
            SceneManager.SetActiveScene(mainActiveScene);
        }

        Debug.Log($"[DungeonManager] Successfully restarted dungeon: {dungeonSceneName}");

        yield return LoadingScreen.Hide();
    }

    private string GetSceneName(GameObject go)
    {
        if (go == null) return string.Empty;
        var scene = go.scene;
        return scene.IsValid() ? scene.name : string.Empty;
    }

    private void BindCameraToPlayer(GameObject player, string sceneName)
    {
        // 1. Deactivate duplicate Main Camera in "Main" scene to avoid rendering/clearing conflicts
        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid() && mainScene.isLoaded)
        {
            var rootObjects = mainScene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                if (obj.name == "Main Camera" && obj.CompareTag("MainCamera"))
                {
                    obj.SetActive(false);
                    Debug.Log("[DungeonManager] Deactivated Main Camera in Main scene dynamically.");
                }
            }
        }

        // 2. Find the target main camera in the loaded map scene specifically
        Camera targetCam = null;
        var allCameras = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (var cam in allCameras)
        {
            if (cam != null && cam.gameObject != null && cam.gameObject.scene.name == sceneName && cam.CompareTag("MainCamera"))
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
                targetCam = cam;
                break;
            }
        }

        if (targetCam == null)
        {
            targetCam = Camera.main;
        }

        // 3. Ensure the active target camera has CinemachineBrain and snap it to player position
        if (targetCam != null)
        {
            var brain = targetCam.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = targetCam.gameObject.AddComponent<CinemachineBrain>();
                Debug.Log($"[DungeonManager] Dynamically added CinemachineBrain to target camera: {targetCam.name} in scene {sceneName}");
            }

            Vector3 playerPos = player.transform.position;
            targetCam.transform.position = new Vector3(playerPos.x, playerPos.y, targetCam.transform.position.z);
            Debug.Log($"[DungeonManager] Warped target Camera {targetCam.name} to {playerPos}");
        }
        else
        {
            Debug.LogWarning("[DungeonManager] No target camera tagged MainCamera found!");
        }

        // 4. Configure all Cinemachine cameras in the target scene
        var camsList = Resources.FindObjectsOfTypeAll<CinemachineCamera>();
        int boundCount = 0;
        foreach (var cam in camsList)
        {
            if (cam != null && cam.gameObject != null)
            {
                string camSceneName = GetSceneName(cam.gameObject);
                if (camSceneName == sceneName)
                {
                    cam.gameObject.SetActive(true);
                    cam.enabled = true;
                    cam.Priority = 999; // Override other virtual cameras
                    cam.Follow = player.transform;

                    Vector3 playerPos = player.transform.position;
                    cam.transform.position = new Vector3(playerPos.x, playerPos.y, cam.transform.position.z);
                    boundCount++;
                }
                else if (camSceneName == "Main")
                {
                    cam.Priority = 10;
                }
            }
        }
        Debug.Log($"[DungeonManager] Configured {boundCount} CinemachineCamera(s) in target scene.");

        // 5. Update minimap camera
        var minimapCam = FindFirstObjectByType<MinimapCameraController>();
        if (minimapCam != null)
        {
            minimapCam.gameObject.SetActive(true);
            minimapCam.enabled = true;
            minimapCam.InitializeMinimap(player.transform);
            Debug.Log("[DungeonManager] Re-initialized MinimapCameraController successfully.");
        }
    }
}
