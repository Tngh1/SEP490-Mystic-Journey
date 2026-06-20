using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;

public class MapTest : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Transform player;

    private IEnumerator Start()
    {
        yield return null;

        Debug.Log("===== MAP TEST START =====");
        Debug.Log($"Scene Object: {gameObject.scene.name}");
        Debug.Log($"WorldState Map: {WorldState.CurrentMapName}");
        Debug.Log($"WorldState Pos: {WorldState.LastPosition}");

        if (IsCorrectMap())
            TryTeleportPlayer();
        else
            Debug.LogWarning("[MapTest] Scene does not match WorldState, skip teleport.");
    }

    private bool IsCorrectMap()
    {
        return WorldState.CurrentMapName == gameObject.scene.name;
    }

    private void TryTeleportPlayer()
    {
        if (player == null)
        {
            Debug.LogError("[MapTest] Player Transform is not assigned.");
            return;
        }

        if (WorldState.LastPosition == Vector3.zero)
        {
            Debug.LogWarning("[MapTest] No saved position, keeping current player position.");
            return;
        }

        player.position = WorldState.LastPosition;
        Debug.Log($"[MapTest] Teleport OK: {WorldState.LastPosition}");

        var minimapCam = FindFirstObjectByType<MinimapCameraController>();
        if (minimapCam != null)
            minimapCam.InitializeMinimap(player.transform);
        else
            Debug.LogWarning("[MapTest] MinimapCameraController not found.");
    }

    public void ForceTeleport()
    {
        Debug.Log("[MapTest] Force Teleport Called");

        if (IsCorrectMap())
            TryTeleportPlayer();
        else
            Debug.LogWarning("[MapTest] ForceTeleport called on the wrong scene.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            PrintDebugInfo();

        if (Input.GetKeyDown(KeyCode.F2))
            SaveState();
    }

    private void PrintDebugInfo()
    {
        Debug.Log("===== DEBUG INFO =====");
        Debug.Log($"Scene Object: {gameObject.scene.name}");
        Debug.Log($"WorldState Map: {WorldState.CurrentMapName}");
        Debug.Log($"WorldState Pos: {WorldState.LastPosition}");

        if (player != null)
            Debug.Log($"Player Pos: {player.position}");
    }

    private void SaveState()
    {
        if (player == null)
        {
            Debug.LogError("[MapTest] Cannot save without player.");
            return;
        }

        WorldState.CurrentMapName = gameObject.scene.name;
        WorldState.LastPosition = player.position;

        PlayerPrefs.SetString(ApiConfig.LastMapNameKey, WorldState.CurrentMapName);
        PlayerPrefs.SetFloat(ApiConfig.PositionXKey, WorldState.LastPosition.x);
        PlayerPrefs.SetFloat(ApiConfig.PositionYKey, WorldState.LastPosition.y);
        PlayerPrefs.Save();

        Debug.Log($"[MapTest] Saved local state | Map={WorldState.CurrentMapName} | Pos={WorldState.LastPosition}");

        if (ApiClient.Instance.HasToken())
        {
            WorldApi.Instance.UpdatePosition(
                WorldState.CurrentMapName,
                WorldState.LastPosition,
                _ => Debug.Log("[MapTest] Position saved to backend."),
                error => Debug.LogWarning($"[MapTest] Save position failed: {error.Message}")
            );
        }
    }
}
