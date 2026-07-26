using System;
using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject knightPrefab;
    [SerializeField] private GameObject magePrefab;
    [SerializeField] private GameObject archerPrefab;

    [Header("Skin")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

    [SerializeField] private Transform spawnPoint;
    public Transform SpawnPoint => spawnPoint;

    private bool _loggedMissingSkinDatabase;

    private IEnumerator Start()
    {
        yield return HydrateWorldStateBeforeSpawn();
        SpawnPlayer();
    }

    private void OnEnable()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnDisconnected += HandleDisconnected;
        }
    }

    private void OnDisable()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnDisconnected -= HandleDisconnected;
        }
    }

    private void HandleDisconnected()
    {
        Debug.Log("[PlayerSpawner] Photon disconnected - respawning local fallback player.");
        StartCoroutine(RespawnNextFrame());
    }

    private IEnumerator RespawnNextFrame()
    {
        yield return null;
        SpawnPlayer();
    }

    private IEnumerator HydrateWorldStateBeforeSpawn()
    {
        if (!ApiClient.Instance.HasToken())
            yield break;

        yield return HydrateCharacterProfileBeforeSpawn();
        yield return HydrateWorldPositionBeforeSpawn();
        yield return HydrateEquippedSkinBeforeSpawn();
    }

    private IEnumerator HydrateCharacterProfileBeforeSpawn()
    {
        var done = false;
        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                if (profile != null && !string.IsNullOrWhiteSpace(profile.PlayerClass))
                {
                    WorldState.PlayerClass = profile.PlayerClass;
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

    private IEnumerator HydrateWorldPositionBeforeSpawn()
    {
        var done = false;
        WorldApi.Instance.GetState(
            state =>
            {
                if (state != null)
                {
                    WorldState.PlayerProfileId = state.PlayerProfileId;
                    if (state.Position != null)
                    {
                        if (!string.IsNullOrWhiteSpace(state.Position.MapName))
                        {
                            WorldState.CurrentMapName = state.Position.MapName;
                        }

                        Vector3 dbPos = new Vector3((float)state.Position.PositionX, (float)state.Position.PositionY, 0f);
                        if (ShouldUseSavedPosition(dbPos))
                        {
                            WorldState.LastPosition = dbPos;
                            MapPositionCache.Save(WorldState.CurrentMapName, dbPos);
                        }
                    }
                }
                done = true;
            },
            error =>
            {
                Debug.LogWarning($"[PlayerSpawner] GetState before spawn failed, using fallback spawn. {error.Message}");
                done = true;
            }
        );

        yield return new WaitUntil(() => done);
    }

    private IEnumerator HydrateEquippedSkinBeforeSpawn()
    {
        var done = false;
        InventoryApi.Instance.GetInventory(
            response =>
            {
                var equippedSkinId = 0;
                if (response != null && response.PlayerSkins != null)
                {
                    foreach (var skin in response.PlayerSkins)
                    {
                        if (skin.IsEquipped)
                        {
                            equippedSkinId = skin.SkinId;
                            break;
                        }
                    }
                }

                WorldState.EquippedSkinId = equippedSkinId;
                WorldState.SaveToPlayerPrefs();
                done = true;
            },
            error =>
            {
                Debug.LogWarning($"[PlayerSpawner] GetInventory failed: {error.Message}");
                done = true;
            }
        );

        yield return new WaitUntil(() => done);
    }

    private void SpawnPlayer()
    {
        if (PhotonManager.Instance != null && PhotonManager.Instance.IsDungeonSession)
        {
            Debug.Log("[PlayerSpawner] Dungeon session active - skipping local spawn; NetworkPlayer will own the avatar.");
            return;
        }

        if (FindFirstObjectByType<PlayerMovement>() != null)
            return;

        var playerClass = string.IsNullOrWhiteSpace(WorldState.PlayerClass)
            ? "Knight"
            : WorldState.PlayerClass.Trim();

        GameObject basePrefab = ResolveBasePrefab(playerClass);
        GameObject prefab = ResolveSkinPrefab(basePrefab);

        if (prefab == null)
        {
            Debug.LogError($"[PlayerSpawner] Missing player prefab for class {playerClass}.");
            return;
        }

        var fallbackSpawn = spawnPoint != null ? spawnPoint.position : new Vector3(11.9f, 17.8f, 0f);
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

    public void RespawnWithSkin()
    {
        var existingPlayer = FindFirstObjectByType<PlayerMovement>();
        if (existingPlayer != null && existingPlayer.GetComponent<Fusion.NetworkObject>() == null)
        {
            WorldState.LastPosition = existingPlayer.transform.position;
            Destroy(existingPlayer.gameObject);
            Debug.Log("[PlayerSpawner] Destroyed old local player for Skin Respawn.");
        }

        StartCoroutine(RespawnNextFrame());
    }

    private GameObject ResolveBasePrefab(string playerClass)
    {
        if (string.Equals(playerClass, "Mage", StringComparison.OrdinalIgnoreCase))
            return magePrefab;
        if (string.Equals(playerClass, "Archer", StringComparison.OrdinalIgnoreCase))
            return archerPrefab;
        return knightPrefab;
    }

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

    private void FollowPlayerWithSceneCamera(Transform player)
    {
        var cam = FindSceneComponent<CinemachineCamera>(gameObject.scene) ?? FindFirstObjectByType<CinemachineCamera>();
        if (cam != null)
            cam.Follow = player;
        else
            Debug.LogWarning("[PlayerSpawner] CinemachineCamera not found for player follow.");
    }

    private static void InitializeMinimap(Transform player)
    {
        var minimapCam = FindFirstObjectByType<MinimapCameraController>();
        if (minimapCam != null)
            minimapCam.InitializeMinimap(player);
    }

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

    private static void EnsurePositionSync(GameObject player)
    {
        if (player != null && player.GetComponent<PlayerWorldPositionSync>() == null)
            player.AddComponent<PlayerWorldPositionSync>();
    }

    private static void EnsureWorldInteractor(GameObject player)
    {
        if (player != null && player.GetComponent<PlayerWorldInteractor>() == null)
            player.AddComponent<PlayerWorldInteractor>();
    }

    private static bool ShouldUseSavedPosition(Vector3 position)
    {
        if (position == Vector3.zero)
            return false;

        return IsFinite(position.x) && IsFinite(position.y) && IsFinite(position.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
