using UnityEngine;
using UnityEngine.Video;

// Executes map teleport portal operation.
public class BoatVideoTeleporter : MapTeleportPortal
{
    [Header("Video Settings")]
    [Tooltip("Gắn VideoPlayer vào đây. Clip sẽ được chiếu SAU khi đã sang map mới.")]
    public UnityEngine.Video.VideoPlayer videoPlayer;

    [Tooltip("Sự kiện xảy ra khi vừa bấm E lên thuyền (SFX, animation thuyền...)")]
    public UnityEngine.Events.UnityEvent onBoardBoat;

    [HideInInspector] public float delayBeforeVideo = 3f;

    private bool isTeleportingWithVideo = false;

    // Executes on trigger enter operation.
    private void OnTriggerEnter(Collider other) { }
    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D other) { }

    // Performs startup initialization for BoatVideoTeleporter on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (mapSceneController == null)
            mapSceneController = FindFirstObjectByType<MapSceneController>();

        if (videoPlayer != null)
            videoPlayer.playOnAwake = false;
    }

    // Executes interact with boat operation.
    public void InteractWithBoat()
    {
        if (isTeleportingWithVideo) return;
        isTeleportingWithVideo = true;

        if (onBoardBoat != null) onBoardBoat.Invoke();

        PlayerPrefs.SetInt("JustUsedBoat", 1);
        PlayerPrefs.Save();

        if (mapSceneController == null)
            mapSceneController = FindFirstObjectByType<MapSceneController>();

        BoatVoyageSequence.Begin(
            mapSceneController,
            targetMapData,
            useSpecificSpawn ? specificSpawnPosition : (Vector3?)null,
            videoPlayer);

        isTeleportingWithVideo = false;
    }
}
