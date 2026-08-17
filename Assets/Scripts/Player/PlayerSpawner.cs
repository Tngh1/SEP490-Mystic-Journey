using System;
using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Executes mono behaviour operation.
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject knightPrefab;
    [SerializeField] private GameObject magePrefab;
    [SerializeField] private GameObject archerPrefab;

    [Header("Skin")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

    [SerializeField] private Transform spawnPoint;
    // Executes spawn point operation.
    public Transform SpawnPoint => spawnPoint;

    private bool _loggedMissingSkinDatabase;

    // Performs startup initialization for PlayerSpawner on the first active frame.
    private IEnumerator Start()
    {
        yield return HydrateWorldStateBeforeSpawn(); // Query latest profile, position, and skin from backend before spawning
        PlayerPresence.RefreshLocal(); // Notify presence manager of avatar spawn
        SpawnPlayer(); // Instantiate local player avatar prefab
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnDisconnected += HandleDisconnected; // Subscribe network disconnect event
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnDisconnected -= HandleDisconnected; // Unsubscribe disconnect event
        }
    }

    // Handles Photon disconnect by triggering local offline avatar respawn.
    private void HandleDisconnected()
    {
        Debug.Log("[PlayerSpawner] Photon disconnected - respawning local fallback player.");
        StartCoroutine(RespawnNextFrame()); // Respawn local non-networked avatar in the next frame
    }

    // Yields one frame to allow teardown before re-instantiating the avatar.
    private IEnumerator RespawnNextFrame()
    {
        yield return null; // Wait one frame for old objects to destroy
        SpawnPlayer(); // Respawn player
    }

    // Coordinates asynchronous API calls to load profile, map coordinates, and equipped skin.
    private IEnumerator HydrateWorldStateBeforeSpawn()
    {
        if (!ApiClient.Instance.HasToken())
            yield break; // Skip network sync if running offline/unauthenticated

        var profileDone = false;
        var positionDone = false;
        var skinDone = false;

        StartCoroutine(RunAndSignal(HydrateCharacterProfileBeforeSpawn(), () => profileDone = true)); // Load class and profile
        StartCoroutine(RunAndSignal(HydrateWorldPositionBeforeSpawn(), () => positionDone = true)); // Load last saved world position
        StartCoroutine(RunAndSignal(HydrateEquippedSkinBeforeSpawn(), () => skinDone = true)); // Load equipped skin ID

        yield return new WaitUntil(() => profileDone && positionDone && skinDone); // Wait until all 3 fetch tasks finish
    }

    // Helper coroutine that awaits a task and signals completion via callback.
    private static IEnumerator RunAndSignal(IEnumerator routine, Action onComplete)
    {
        yield return routine;
        onComplete?.Invoke();
    }

    // Queries player class and persists it in WorldState.
    private IEnumerator HydrateCharacterProfileBeforeSpawn()
    {
        var done = false;
        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                if (profile != null && !string.IsNullOrWhiteSpace(profile.PlayerClass))
                {
                    WorldState.PlayerClass = profile.PlayerClass; // Cache active class (Knight/Archer/Mage)
                    WorldState.SaveToPlayerPrefs();
                }
                done = true;
            },
            error =>
            {
                Debug.LogWarning($"[PlayerSpawner] GetMyProfile failed: {error.Message}");
                done = true;
            }
        );
        yield return new WaitUntil(() => done);
    }

    // Queries player's last recorded world position and map name from the server.
    private IEnumerator HydrateWorldPositionBeforeSpawn()
    {
        var done = false;
        WorldApi.Instance.GetPosition(
            position =>
            {
                if (position != null)
                {
                    if (!string.IsNullOrWhiteSpace(position.MapName))
                    {
                        WorldState.CurrentMapName = position.MapName; // Update current map name
                    }

                    Vector3 dbPos = new Vector3((float)position.PositionX, (float)position.PositionY, 0f);
                    if (ShouldUseSavedPosition(dbPos))
                    {
                        WorldState.LastPosition = dbPos; // Apply saved world coordinates
                        MapPositionCache.Save(WorldState.CurrentMapName, dbPos);
                    }
                }
                done = true;
            },
            error =>
            {
                Debug.LogWarning($"[PlayerSpawner] GetPosition before spawn failed, using fallback spawn. {error.Message}");
                done = true;
            }
        );

        yield return new WaitUntil(() => done);
    }

    // Queries equipped skin ID from player inventory.
    private IEnumerator HydrateEquippedSkinBeforeSpawn()
    {
        var done = false;
        var skinAtRequestStart = WorldState.EquippedSkinId;
        InventoryApi.Instance.GetInventory(
            response =>
            {
                var equippedSkinId = WorldState.EquippedSkinId;
                if (response != null && response.PlayerSkins != null)
                {
                    equippedSkinId = 0;
                    foreach (var skin in response.PlayerSkins)
                    {
                        if (skin != null && skin.IsEquipped)
                        {
                            equippedSkinId = skin.SkinId; // Find equipped skin in inventory response
                            break;
                        }
                    }
                }

                if (WorldState.EquippedSkinId == skinAtRequestStart)
                {
                    WorldState.EquippedSkinId = equippedSkinId; // Persist equipped skin ID
                    WorldState.SaveToPlayerPrefs();
                }
                else
                {
                    Debug.Log($"[PlayerSpawner] Ignored stale inventory skin={equippedSkinId}; keeping newer SkinId={WorldState.EquippedSkinId}.");
                }
                done = true;
            },
            error =>
            {
                Debug.LogWarning($"[PlayerSpawner] GetInventory failed; keeping persisted SkinId={WorldState.EquippedSkinId}. {error.Message}");
                done = true;
            }
        );

        yield return new WaitUntil(() => done);
    }

    // Instantiates character prefab based on player class, binds camera, and applies skins.
    private void SpawnPlayer()
    {
        if (PhotonManager.Instance != null && PhotonManager.Instance.IsDungeonSession)
        {
            Debug.Log("[PlayerSpawner] Dungeon session active - skipping local spawn; NetworkPlayer will own the avatar.");
            return; // In networked multiplayer sessions, NetworkPlayer manages avatar lifecycle
        }

        if (FindFirstObjectByType<PlayerMovement>() != null || FindFirstObjectByType<PlayerEntity>() != null)
            return; // Prevent duplicate player instantiation

        var playerClass = string.IsNullOrWhiteSpace(WorldState.PlayerClass)
            ? "Knight"
            : WorldState.PlayerClass.Trim();

        GameObject basePrefab = ResolveBasePrefab(playerClass);
        GameObject prefab = ResolveSkinPrefab(basePrefab);

        if (spawnPoint == null)
        {
            var taggedMarker = GameObject.FindGameObjectWithTag("PlayerSpawn");
            if (taggedMarker != null)
            {
                spawnPoint = taggedMarker.transform;
            }
        }

        var fallbackSpawn = spawnPoint != null ? spawnPoint.position : new Vector3(24.0889f, -49.7661f, 0f);
        var spawnPosition = ShouldUseSavedPosition(WorldState.LastPosition)
            ? WorldState.LastPosition
            : fallbackSpawn;

        GameObject player = Instantiate(prefab, spawnPosition, Quaternion.identity);

        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(player, mainScene);
        }
        else
        {
            SceneManager.MoveGameObjectToScene(player, gameObject.scene);
        }
        ActivatePlayerInput(player);
        EnsurePositionSync(player);
        EnsureWorldInteractor(player);
        WorldSceneInteractableBootstrap.EnsureForScene(gameObject.scene);
        FollowPlayerWithSceneCamera(player.transform);
        InitializeMinimap(player.transform);

        Debug.Log($"[PlayerSpawner] Spawned {playerClass} at {spawnPosition} | SkinId={WorldState.EquippedSkinId} | Scene={gameObject.scene.name}.");
    }

    // Executes respawn with skin operation.
    public void RespawnWithSkin()
    {
        var existingPlayer = FindOfflinePlayerRoot();
        if (existingPlayer != null)
        {
            WorldState.LastPosition = existingPlayer.transform.position;
            Destroy(existingPlayer.gameObject);
            Debug.Log("[PlayerSpawner] Destroyed old local player for Skin Respawn.");
        }

        CleanupLegacyVisualOnlyPlayers();

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(RespawnNextFrame());
    }

    // Executes find offline player root operation.
    private static GameObject FindOfflinePlayerRoot()
    {
        var entity = FindFirstObjectByType<PlayerEntity>();
        if (entity != null && entity.GetComponent<Fusion.NetworkObject>() == null)
            return entity.gameObject;

        var movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null && movement.GetComponent<Fusion.NetworkObject>() == null)
            return movement.gameObject;

        return null;
    }

    // Executes cleanup legacy visual only players operation.
    private static void CleanupLegacyVisualOnlyPlayers()
    {
        var taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        for (var i = 0; i < taggedPlayers.Length; i++)
        {
            var candidate = taggedPlayers[i];
            if (candidate == null || candidate.transform.parent != null)
                continue;
            if (candidate.GetComponent<Fusion.NetworkObject>() != null)
                continue;
            if (candidate.GetComponent<PlayerMovement>() != null || candidate.GetComponent<PlayerEntity>() != null)
                continue;

            Destroy(candidate);
            Debug.Log($"[PlayerSpawner] Removed stale visual-only player '{candidate.name}'.");
        }
    }

    // Executes resolve base prefab operation.
    private GameObject ResolveBasePrefab(string playerClass)
    {
        if (string.Equals(playerClass, "Mage", StringComparison.OrdinalIgnoreCase))
            return magePrefab;
        if (string.Equals(playerClass, "Archer", StringComparison.OrdinalIgnoreCase))
            return archerPrefab;
        return knightPrefab;
    }

    // Executes resolve skin prefab operation.
    private GameObject ResolveSkinPrefab(GameObject fallbackPrefab)
    {
        int skinId = WorldState.EquippedSkinId;
        if (skinId <= 0)
            return fallbackPrefab;

        var database = EnsureSkinDatabase();
        if (database == null)
            return fallbackPrefab;

        if (!database.TryGetSkinData(skinId, out var skinData))
        {
            Debug.LogWarning($"[PlayerSpawner] SkinId={skinId} is not mapped in '{database.name}'. Falling back to class prefab.", database);
            return fallbackPrefab;
        }

        if (skinData.prefab == null)
        {
            Debug.LogWarning($"[PlayerSpawner] SkinId={skinId} is mapped in '{database.name}' but has no prefab. Falling back to class prefab.", database);
            return fallbackPrefab;
        }

        return skinData.prefab;
    }

    // Executes ensure skin database operation.
    private SkinDatabaseSO EnsureSkinDatabase()
    {
        if (skinDatabase == null)
            skinDatabase = SkinDatabaseSO.LoadDefault();

        if (skinDatabase == null && !_loggedMissingSkinDatabase)
        {
            _loggedMissingSkinDatabase = true;
            Debug.LogWarning("[PlayerSpawner] SkinDatabaseSO is not assigned and no default SkinDatabase could be loaded. Skin ids will fall back to class prefabs.", this);
        }

        return skinDatabase;
    }

    // Executes follow player with scene camera operation.
    private void FollowPlayerWithSceneCamera(Transform player)
    {
        var cam = FindSceneComponent<CinemachineCamera>(gameObject.scene) ?? FindFirstObjectByType<CinemachineCamera>();
        if (cam != null)
        {
            cam.Follow = player;
            var composer = cam.GetComponent<CinemachinePositionComposer>();
            if (composer != null)
            {
                composer.Damping = new Vector3(0.05f, 0.05f, 0.05f);
            }
        }
        else
            Debug.LogWarning("[PlayerSpawner] CinemachineCamera not found for player follow.");
    }

    // Executes initialize minimap operation.
    private static void InitializeMinimap(Transform player)
    {
        var minimapCam = FindFirstObjectByType<MinimapCameraController>();
        if (minimapCam != null)
            minimapCam.InitializeMinimap(player);
    }

    // Executes component operation.
    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        var components = Resources.FindObjectsOfTypeAll<T>();
        foreach (var component in components)
        {
            if (component != null && component.gameObject.scene == scene)
                return component;
        }

        return null;
    }

    // Executes activate player input operation.
    private static void ActivatePlayerInput(GameObject player)
    {
        if (player == null)
            return;

        var playerInput = player.GetComponent<PlayerInput>();
        if (playerInput == null)
            return;

        playerInput.defaultActionMap = "Player";
        playerInput.ActivateInput();

        if (playerInput.currentActionMap == null || playerInput.currentActionMap.name != "Player")
            playerInput.SwitchCurrentActionMap("Player");
    }

    // Executes ensure position sync operation.
    private static void EnsurePositionSync(GameObject player)
    {
        if (player != null && player.GetComponent<PlayerWorldPositionSync>() == null)
            player.AddComponent<PlayerWorldPositionSync>();
    }

    // Executes ensure world interactor operation.
    private static void EnsureWorldInteractor(GameObject player)
    {
        if (player != null && player.GetComponent<PlayerWorldInteractor>() == null)
            player.AddComponent<PlayerWorldInteractor>();
    }

    // Executes should use saved position operation.
    private static bool ShouldUseSavedPosition(Vector3 position)
    {
        if (position == Vector3.zero)
            return false;

        return IsFinite(position.x) && IsFinite(position.y) && IsFinite(position.z);
    }

    // Executes is finite operation.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
