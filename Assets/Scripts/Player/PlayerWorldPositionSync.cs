using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;

// Executes mono behaviour operation.
public class PlayerWorldPositionSync : MonoBehaviour
{
    [SerializeField] private float saveInterval = 2f;
    [SerializeField] private float saveDistance = 0.25f;

    private Vector3 lastSavedPosition;
    private float nextSaveTime;
    private bool saving;
    private bool mapTransitionInProgress;

    // Executes has pending save operation.
    public bool HasPendingSave => saving;

    // Executes begin map transition operation.
    public void BeginMapTransition()
    {
        mapTransitionInProgress = true;
    }

    // Executes complete map transition operation.
    public void CompleteMapTransition(Vector3 authoritativePosition)
    {
        lastSavedPosition = authoritativePosition;
        nextSaveTime = Time.time + saveInterval;
        mapTransitionInProgress = false;
        CacheLocalPosition(authoritativePosition, saveToPrefs: true);
    }

    // Performs startup initialization for PlayerWorldPositionSync on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        lastSavedPosition = transform.position;
        CacheLocalPosition(transform.position, saveToPrefs: false);
    }

    // Per-frame update loop for PlayerWorldPositionSync.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        if (mapTransitionInProgress)
            return;

        var currentPosition = transform.position;
        CacheLocalPosition(currentPosition, saveToPrefs: false);

        if (Time.time < nextSaveTime || saving)
            return;

        if ((currentPosition - lastSavedPosition).sqrMagnitude < saveDistance * saveDistance)
            return;

        nextSaveTime = Time.time + saveInterval;
        CacheLocalPosition(currentPosition, saveToPrefs: true);

        if (!ApiClient.Instance.HasToken())
        {
            lastSavedPosition = currentPosition;
            return;
        }

        saving = true;
        var mapName = string.IsNullOrWhiteSpace(WorldState.CurrentMapName) ? "ElfForest" : WorldState.CurrentMapName;
        WorldApi.Instance.UpdatePosition(
            mapName,
            currentPosition,
            _ =>
            {
                lastSavedPosition = currentPosition;
                saving = false;
            },
            error =>
            {
                Debug.LogWarning($"[PlayerWorldPositionSync] Save position failed: {error.Message}");
                saving = false;
            }
        );
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        CacheLocalPosition(transform.position, saveToPrefs: true);
    }

    // Executes cache local position operation.
    // Validates input parameters against null or empty values.
    private static void CacheLocalPosition(Vector3 position, bool saveToPrefs)
    {
        if (string.IsNullOrWhiteSpace(WorldState.CurrentMapName))
            WorldState.CurrentMapName = "ElfForest";

        WorldState.LastPosition = new Vector3(position.x, position.y, 0f);

        if (!saveToPrefs)
            return;

        MapPositionCache.Save(WorldState.CurrentMapName, WorldState.LastPosition);

        PlayerPrefs.SetString(ApiConfig.LastMapNameKey, WorldState.CurrentMapName);
        PlayerPrefs.SetFloat(ApiConfig.PositionXKey, position.x);
        PlayerPrefs.SetFloat(ApiConfig.PositionYKey, position.y);
        PlayerPrefs.Save();
    }
}
