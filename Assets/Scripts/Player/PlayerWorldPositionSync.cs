using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;

public class PlayerWorldPositionSync : MonoBehaviour
{
    [SerializeField] private float saveInterval = 2f;
    [SerializeField] private float saveDistance = 0.25f;

    private Vector3 lastSavedPosition;
    private float nextSaveTime;
    private bool saving;

    private void Start()
    {
        lastSavedPosition = transform.position;
        CacheLocalPosition(transform.position, saveToPrefs: false);
    }

    private void Update()
    {
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

    private void OnDisable()
    {
        CacheLocalPosition(transform.position, saveToPrefs: true);
    }

    private static void CacheLocalPosition(Vector3 position, bool saveToPrefs)
    {
        if (string.IsNullOrWhiteSpace(WorldState.CurrentMapName))
            WorldState.CurrentMapName = "ElfForest";

        WorldState.LastPosition = new Vector3(position.x, position.y, 0f);

        if (!saveToPrefs)
            return;

        PlayerPrefs.SetString(ApiConfig.LastMapNameKey, WorldState.CurrentMapName);
        PlayerPrefs.SetFloat(ApiConfig.PositionXKey, position.x);
        PlayerPrefs.SetFloat(ApiConfig.PositionYKey, position.y);
        PlayerPrefs.Save();
    }
}
