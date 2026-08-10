using UnityEngine;
using UnityEngine.Video;

public class BoatVideoTeleporter : MapTeleportPortal
{
    [Header("Video Settings")]
    [Tooltip("Gắn VideoPlayer vào đây. Clip sẽ được chiếu SAU khi đã sang map mới.")]
    public UnityEngine.Video.VideoPlayer videoPlayer;

    [Tooltip("Sự kiện xảy ra khi vừa bấm E lên thuyền (SFX, animation thuyền...)")]
    public UnityEngine.Events.UnityEvent onBoardBoat;

    // ponytail: delayBeforeVideo giữ lại để không mất giá trị đã set trong scene, nhưng KHÔNG còn
    // được dùng: 3 giây "chèo thuyền" chính là quãng người chơi thấy nhân vật biến mất và không có
    // phản hồi gì. Muốn có màn chèo thuyền thật thì làm animation trong lúc loading đang che.
    [HideInInspector] public float delayBeforeVideo = 3f;

    private bool isTeleportingWithVideo = false;

    // Chặn tính năng chạm vào là bay luôn của cổng dịch chuyển cũ
    private void OnTriggerEnter(Collider other) { }
    private void OnTriggerEnter2D(Collider2D other) { }

    private void Start()
    {
        if (mapSceneController == null)
            mapSceneController = FindFirstObjectByType<MapSceneController>();

        if (videoPlayer != null)
            videoPlayer.playOnAwake = false;
    }

    // Hàm này sẽ được gọi khi bạn đứng gần thuyền và bấm phím E
    public void InteractWithBoat()
    {
        if (isTeleportingWithVideo) return;
        isTeleportingWithVideo = true;

        if (onBoardBoat != null) onBoardBoat.Invoke();

        PlayerPrefs.SetInt("JustUsedBoat", 1);
        PlayerPrefs.Save();

        if (mapSceneController == null)
            mapSceneController = FindFirstObjectByType<MapSceneController>();

        // Toàn bộ trình tự nằm ở BoatVoyageSequence (object DontDestroyOnLoad). KHÔNG chạy coroutine
        // ở đây: thuyền thuộc scene AutumnPumpkin và bị unload giữa lúc đổi map, coroutine chết theo
        // -> đó là lý do trước đây video/dịch chuyển hay đứt đoạn.
        //
        // Cũng KHÔNG còn ẩn sprite người chơi + gắn thuyền vào player + chờ 3 giây: đúng quãng đó là
        // lúc người chơi thấy "nhân vật mất tiêu" mà chưa có loading hay video nào hiện ra. Giờ bấm E
        // là hiện loading ngay.
        BoatVoyageSequence.Begin(
            mapSceneController,
            targetMapData,
            useSpecificSpawn ? specificSpawnPosition : (Vector3?)null,
            videoPlayer);

        isTeleportingWithVideo = false;
    }
}
