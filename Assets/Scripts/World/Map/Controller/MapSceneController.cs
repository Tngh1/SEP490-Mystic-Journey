using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapSceneController : MonoBehaviour
{
    [SerializeField]
    private List<MapSceneConfig> mapConfigs;

    /// <summary>
    /// Đang trong dungeon thì không cho đổi map. Guard đặt ở đây (chứ không ở UI) để bịt MỌI
    /// đường vào: map panel, cổng dịch chuyển, thuyền.
    ///
    /// Lý do phải chặn, không phải chỉ vì thiết kế: trong dungeon, WorldState.CurrentMapName là
    /// tên scene dungeon và DungeonManager đã move player VÀO scene đó, nên ChangeMap sẽ unload
    /// chính scene đang chứa player -> player bị destroy, tới map mới không còn nhân vật. Muốn
    /// ra ngoài phải đi qua DungeonManager.ReturnToWorldMap (rời phòng Photon, trả player về
    /// Main trước khi unload, tắt dungeon HUD, thu hồi rương thưởng).
    /// </summary>
    public bool IsTravelBlocked
    {
        get { return IsTravelBlockedNow; }
    }

    /// <summary>
    /// Bản static của <see cref="IsTravelBlocked"/>, cho UI gọi mà không cần tìm instance:
    /// map panel nằm ở Canvas của Main còn MapManager là một GameObject khác, và các đường
    /// mở panel (phím M, nút MiniMap) chạy trước cả khi người chơi chọn map nào.
    /// </summary>
    public static bool IsTravelBlockedNow
    {
        get { return DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon; }
    }

    public void EnterMap(MapData mapData, bool useCache = true, Vector3? specificSpawnPos = null)
    {
        if (IsTravelBlocked)
        {
            Debug.Log("[MapSceneController] Travel blocked: player is inside a dungeon.");
            return;
        }

        MapSceneConfig config =
            mapConfigs.Find(
                x => x != null && x.mapData == mapData);

        if (config == null)
        {
            Debug.LogError(
                $"Config not found for {mapData.mapName}");
            return;
        }

        StartCoroutine(
            ChangeMap(config.sceneName, useCache, specificSpawnPos));
    }

    private IEnumerator ChangeMap(
        string targetScene, bool useCache = true, Vector3? specificSpawnPos = null)
    {
        if (targetScene ==
            WorldState.CurrentMapName)
        {
            yield break;
        }

        // Bật màn hình loading TRƯỚC khi unload: nếu bật sau, người chơi thấy một frame
        // scene trống + player rơi ở toạ độ cũ.
        yield return LoadingScreen.Show();

        var previousScene = WorldState.CurrentMapName;
        if (!string.IsNullOrWhiteSpace(previousScene) && SceneManager.GetSceneByName(previousScene).isLoaded)
        {
            // Luồng thường PlayerSpawner đã đặt player trong "Main" nên unload map cũ không
            // đụng tới nó. Vẫn đưa về Main phòng khi một luồng khác gắn player vào scene map —
            // unload nhầm là mất nhân vật, không phải chỉ sai vị trí.
            MovePlayerToMainScene();

            yield return SceneManager
                .UnloadSceneAsync(previousScene);
        }

        LoadingProgress.Report(0.3f, "Loading map...");
        var loadOp = SceneManager
            .LoadSceneAsync(
                targetScene,
                LoadSceneMode.Additive);
        while (loadOp != null && !loadOp.isDone)
        {
            LoadingProgress.Report(Mathf.Lerp(0.3f, 0.85f, loadOp.progress), "Loading map...");
            yield return null;
        }

        Vector3 spawnPosition = Vector3.zero;
        bool positionFound = false;

        if (specificSpawnPos.HasValue)
        {
            spawnPosition = specificSpawnPos.Value;
            positionFound = true;
            Debug.Log($"[MapSceneController] Using specific spawn pos {spawnPosition}");
        }
        else if (useCache && MapPositionCache.TryGet(targetScene, out var cachedPos) && cachedPos != Vector3.zero)
        {
            spawnPosition = cachedPos;
            positionFound = true;
            Debug.Log($"[MapSceneController] Returning to {targetScene} at last pos {spawnPosition}");
        }
        else
        {
            // First, try to find a PlayerSpawner in the newly loaded scene
            var spawners = Object.FindObjectsByType<PlayerSpawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                if (spawner.gameObject.scene.name == targetScene && spawner.SpawnPoint != null)
                {
                    spawnPosition = spawner.SpawnPoint.position;
                    positionFound = true;
                    Debug.Log($"[MapSceneController] Found PlayerSpawner in {targetScene}. Spawn: {spawnPosition}");
                    break;
                }
            }

            if (!positionFound)
            {
                var spawnMarker = GameObject.FindGameObjectWithTag("PlayerSpawn");
                if (spawnMarker == null) spawnMarker = GameObject.Find("PlayerSpawn");
                if (spawnMarker == null) spawnMarker = GameObject.Find("PlayerSpawnRuntime");

                spawnPosition = spawnMarker != null ? spawnMarker.transform.position : Vector3.zero;

                if (spawnMarker != null)
                    Debug.Log($"[MapSceneController] First visit {targetScene} at PlayerSpawn {spawnPosition}");
                else
                    Debug.LogWarning($"[MapSceneController] No 'PlayerSpawn' in {targetScene}, using default.");
            }
        }

        WorldState.CurrentMapName = targetScene;
        WorldState.LastPosition = spawnPosition;
        WorldState.SaveToPlayerPrefs();
        MapPositionCache.Save(targetScene, spawnPosition);

        WorldRuntimeEvents.RaiseMapChanged(targetScene);
        WorldRuntimeEvents.RaiseQuestsChanged();
        // KHÔNG LoadMyQuests() ở đây: BE chỉ tạo bản ghi quest NotStarted cho map bằng
        // profile.LastMapName (PlayerQuestService.GetMyQuests), mà LastMapName chỉ được cập nhật
        // bởi UpdatePosition bên dưới. Gọi sớm sẽ nhận lại quest của map CŨ -> vào map mới không
        // có nhiệm vụ và không có mũi tên waypoint. Load sau khi lưu vị trí thành công.

        var player = FindPlayer();
        if (player == null)
            Debug.LogWarning($"[MapSceneController] No player found after loading {targetScene}.");

        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = spawnPosition;
            }
            player.transform.position = spawnPosition;
            Physics2D.SyncTransforms();

            // Set camera follow target for the newly loaded map scene
            var vcam = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
            {
                vcam.Follow = player.transform;
                var composer = vcam.GetComponent<Unity.Cinemachine.CinemachinePositionComposer>();
                if (composer != null)
                {
                    composer.Damping = new Vector3(0.05f, 0.05f, 0.05f);
                }
            }

            // Re-initialize minimap for the newly loaded map scene
            var minimapCam = Object.FindFirstObjectByType<MinimapCameraController>();
            if (minimapCam != null)
            {
                minimapCam.InitializeMinimap(player.transform);
            }
        }

        if (ApiClient.Instance.HasToken())
        {
            WorldApi.Instance.UpdatePosition(
                targetScene,
                spawnPosition,
                _ =>
                {
                    Debug.Log($"[MapSceneController] Saved: {targetScene} @ {spawnPosition}");
                    WorldRuntimeEvents.RaiseMapChanged(targetScene);
                    // LastMapName đã là map mới -> giờ GetMyQuests mới trả về quest của map này.
                    // HandleLoadedQuestResponses tự bắn QuestsChanged để panel + waypoint render lại.
                    QuestManager.Instance?.LoadMyQuests();
                },
                error =>
                {
                    Debug.LogWarning($"[MapSceneController] Save failed: {error.Message}");
                    // Lưu vị trí thất bại vẫn phải thử nạp quest, nếu không HUD trắng hoàn toàn.
                    QuestManager.Instance?.LoadMyQuests();
                }
            );
        }

        // ponytail: KHÔNG chờ UpdatePosition/LoadMyQuests xong mới tắt loading — quest nạp
        // xong sẽ tự render lại HUD. Nếu sau này muốn vào map là có ngay quest thì đổi callback
        // ở trên thành cờ và chờ nó trước LoadingScreen.Hide().
        yield return LoadingScreen.Hide();
    }

    /// <summary>
    /// Local player object, luôn trả về ROOT của scene.
    /// Ưu tiên NetworkPlayer.Local: trong multiplayer, PlayerMovement có thể khớp nhầm child
    /// VISUAL của class prefab, mà child không phải scene root nên MoveGameObjectToScene ném lỗi.
    /// </summary>
    private static GameObject FindPlayer()
    {
        GameObject found = NetworkPlayer.Local != null ? NetworkPlayer.Local.gameObject : null;

        found ??= GameObject.FindGameObjectWithTag("Player");

        if (found == null)
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) found = pm.gameObject;
        }

        return found != null ? found.transform.root.gameObject : null;
    }

    /// <summary>
    /// Đưa player về scene "Main" để nó sống sót qua lần unload map cũ. Nuốt lỗi có chủ đích:
    /// MoveGameObjectToScene ném nếu object không phải scene root, và một lần move hỏng không
    /// đáng để giết coroutine đổi map (sẽ kẹt màn hình loading).
    /// </summary>
    private static void MovePlayerToMainScene()
    {
        var player = FindPlayer();
        if (player == null) return;

        var mainScene = SceneManager.GetSceneByName("Main");
        if (!mainScene.IsValid() || !mainScene.isLoaded || player.scene == mainScene) return;

        try
        {
            SceneManager.MoveGameObjectToScene(player, mainScene);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MapSceneController] Could not move '{player.name}' to Main: {ex.Message}");
        }
    }
}
