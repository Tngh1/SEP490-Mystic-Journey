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

    [SerializeField] private Transform spawnPoint;

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

    /// <summary>
    /// Fusion despawns the networked avatar as part of runner shutdown, which leaves the
    /// player with no controllable character. Spawn a non-networked fallback avatar back
    /// in, the same way the initial (pre-connect) flow does.
    ///
    /// PhotonManager now awaits Fusion's own Shutdown Task before raising OnDisconnected,
    /// but Unity's Destroy() is itself deferred to the end of the current frame — so the
    /// old NetworkPlayer's GameObject can still exist for one more frame after Shutdown's
    /// Task completes. Wait a frame here so SpawnPlayer()'s "is there already a player?"
    /// guard doesn't see the stale, about-to-be-destroyed object and skip the respawn.
    /// </summary>
    private void HandleDisconnected()
    {
        Debug.Log("[PlayerSpawner] Photon disconnected — respawning local fallback player.");
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

        var needsWorldState = string.IsNullOrWhiteSpace(WorldState.CurrentMapName) || !ShouldUseSavedPosition(WorldState.LastPosition);
        if (!needsWorldState)
            yield break;

        var done = false;
        WorldApi.Instance.GetState(
            state =>
            {
                if (state != null)
                    WorldState.PlayerProfileId = state.PlayerProfileId;
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

    private void SpawnPlayer()
    {
        // In multiplayer, the player avatar is a Fusion NetworkObject spawned by
        // PhotonManager (each client spawns its own). Spawning a second, non-networked
        // local player here would be invisible to other clients and would fight the
        // network avatar for the camera. Skip the local spawn entirely when connected.
        if (PhotonManager.Instance != null && PhotonManager.Instance.IsConnected)
        {
            Debug.Log("[PlayerSpawner] Photon connected — skipping local spawn; NetworkPlayer will own the avatar.");
            return;
        }

        if (FindFirstObjectByType<PlayerMovement>() != null)
            return;

        var playerClass = string.IsNullOrWhiteSpace(WorldState.PlayerClass)
            ? "Knight"
            : WorldState.PlayerClass.Trim();

        GameObject prefab = playerClass switch
        {
            "Mage" => magePrefab,
            "Archer" => archerPrefab,
            _ => knightPrefab
        };

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
        SceneManager.MoveGameObjectToScene(player, gameObject.scene);
        ActivatePlayerInput(player);
        EnsurePositionSync(player);
        EnsureWorldInteractor(player);
        WorldSceneInteractableBootstrap.EnsureForScene(gameObject.scene);
        FollowPlayerWithSceneCamera(player.transform);
        InitializeMinimap(player.transform);

        Debug.Log($"[PlayerSpawner] Spawned {playerClass} at {spawnPosition} | WorldStatePos={WorldState.LastPosition} | Scene={gameObject.scene.name}.");
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
