using System.Collections;
using UnityEngine;

public class MapTest : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Transform player;

    private IEnumerator Start()
    {
        // ? ??i 1 frame ?? ??m b?o scene + WorldState ?ã s?n sàng
        yield return null;

        Debug.Log("===== MAP TEST START =====");
        Debug.Log($"Scene Object: {gameObject.scene.name}");
        Debug.Log($"WorldState Map: {WorldState.CurrentMapName}");
        Debug.Log($"WorldState Pos: {WorldState.LastPosition}");

        // ?? So sánh ?úng scene hi?n t?i c?a object (KHÔNG dùng GetActiveScene)
        if (IsCorrectMap())
        {
            TryTeleportPlayer();
        }
        else
        {
            Debug.LogWarning("[MapTest] Scene không kh?p WorldState -> không teleport");
        }
    }

    private bool IsCorrectMap()
    {
        return WorldState.CurrentMapName == gameObject.scene.name;
    }

    private void TryTeleportPlayer()
    {
        if (player == null)
        {
            Debug.LogError("[MapTest] ? Ch?a gán Player Transform!");
            return;
        }

        if (WorldState.LastPosition == Vector3.zero)
        {
            Debug.LogWarning("[MapTest] ? Không có v? trí l?u -> gi? nguyên v? trí");
            return;
        }

        // 1. D?ch chuy?n nhân v?t theo data (hi?n t?i là Test, sau là JSON)
        player.position = WorldState.LastPosition;
        Debug.Log($"? [Teleport OK] Player -> {WorldState.LastPosition}");

        // ---------------- THÊM ?O?N NÀY ----------------
        // 2. B?m d? li?u cho Minimap Camera ? scene Main
        // Dùng FindFirstObjectByType vì 2 object ? 2 scene khác nhau (Additive)
        MinimapCameraController minimapCam = FindFirstObjectByType<MinimapCameraController>();
        if (minimapCam != null)
        {
            minimapCam.InitializeMinimap(player.transform);
        }
        else
        {
            Debug.LogWarning("[MapTest] Không tìm th?y MinimapCameraController trên Scene!");
        }
        // -----------------------------------------------
    }

    // ?? OPTIONAL: cho Bootstrap g?i n?u c?n
    public void ForceTeleport()
    {
        Debug.Log("[MapTest] Force Teleport Called");

        if (IsCorrectMap())
        {
            TryTeleportPlayer();
        }
        else
        {
            Debug.LogWarning("[MapTest] ForceTeleport nh?ng v?n sai scene!");
        }
    }

    // ?? DEBUG HOTKEY
    private void Update()
    {
        // F1: In info
        if (Input.GetKeyDown(KeyCode.F1))
        {
            PrintDebugInfo();
        }

        // F2: Save v? trí hi?n t?i
        if (Input.GetKeyDown(KeyCode.F2))
        {
            SaveState();
        }
    }

    private void PrintDebugInfo()
    {
        Debug.Log("===== DEBUG INFO =====");
        Debug.Log($"Scene Object: {gameObject.scene.name}");
        Debug.Log($"WorldState Map: {WorldState.CurrentMapName}");
        Debug.Log($"WorldState Pos: {WorldState.LastPosition}");

        if (player != null)
        {
            Debug.Log($"Player Pos: {player.position}");
        }
    }

    private void SaveState()
    {
        if (player == null)
        {
            Debug.LogError("[MapTest] Không có player ?? save!");
            return;
        }

        WorldState.CurrentMapName = gameObject.scene.name;
        WorldState.LastPosition = player.position;

        Debug.Log($"?? [Saved] Map: {WorldState.CurrentMapName} | Pos: {WorldState.LastPosition}");
    }
}