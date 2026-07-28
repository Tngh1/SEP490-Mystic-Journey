using UnityEngine;
using UnityEngine.Video;

public class BoatVideoTeleporter : MapTeleportPortal
{
    [Header("Video Settings")]
    [Tooltip("Gắn VideoPlayer vào đây (Nhớ thiết lập Render Mode là Camera Near Plane hoặc UI để che màn hình)")]
    public UnityEngine.Video.VideoPlayer videoPlayer;
    
    [Header("Rowing Sequence")]
    [Tooltip("Thời gian chèo thuyền (chờ) trước khi chiếu Video")]
    public float delayBeforeVideo = 3f;
    
    [Tooltip("Sự kiện xảy ra khi vừa bấm E lên thuyền (Dùng để ẩn Player, bật Animation thuyền...)")]
    public UnityEngine.Events.UnityEvent onBoardBoat;

    private bool isTeleportingWithVideo = false;

    // Chặn tính năng chạm vào là bay luôn của cổng dịch chuyển cũ
    private void OnTriggerEnter(Collider other) { }
    private void OnTriggerEnter2D(Collider2D other) { }

    private void Start()
    {
        if (mapSceneController == null)
            mapSceneController = FindObjectOfType<MapSceneController>();
        
        if (videoPlayer != null)
        {
            // Đảm bảo video không tự chạy lúc mới vào map
            videoPlayer.playOnAwake = false;
            // Lắng nghe sự kiện video chạy xong
            videoPlayer.loopPointReached += OnVideoFinished;
        }
    }

    // Hàm này sẽ được gọi khi bạn đứng gần thuyền và bấm phím E
    public void InteractWithBoat()
    {
        if (isTeleportingWithVideo) return;
        isTeleportingWithVideo = true;

        StartCoroutine(BoatSequenceCoroutine());
    }

    private System.Collections.IEnumerator BoatSequenceCoroutine()
    {
        // Ẩn UI Quest nếu cần
        WorldInteractionPromptRuntime.Hide();

        // 1. Tìm người chơi và cho họ "lái" thuyền
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        SpriteRenderer[] playerSprites = null;
        
        if (player != null)
        {
            // Tạm ẩn hình ảnh của người chơi đi (để trông như đã chui vào thuyền)
            playerSprites = player.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sp in playerSprites)
            {
                sp.enabled = false;
            }

            // Gắn chiếc thuyền dính chặt vào người chơi để người chơi có thể "lái" thuyền đi lòng vòng
            this.transform.SetParent(player.transform);
            this.transform.localPosition = Vector3.zero;
            
            // Xoay mặt người chơi hoặc set animation thuyền ở đây nếu dùng sự kiện
            if (onBoardBoat != null) onBoardBoat.Invoke();
            
            Debug.Log("[Boat] Đã lên thuyền, bạn có thể lái thuyền trong 3 giây...");
        }

        // 2. Chờ thời gian lái thuyền
        if (delayBeforeVideo > 0)
        {
            yield return new WaitForSeconds(delayBeforeVideo);
        }

        // 3. Bật video lên xem
        if (videoPlayer != null && videoPlayer.clip != null)
        {
            MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoStarted(videoPlayer);
            videoPlayer.gameObject.SetActive(true);
            videoPlayer.Play();
            Debug.Log("[Boat] Đang chiếu video...");
            
            // Khôi phục lại người chơi (tùy chọn trước khi load scene)
            if (playerSprites != null)
            {
                foreach (var sp in playerSprites) sp.enabled = true;
            }
        }
        else
        {
            // Khôi phục lại người chơi
            if (playerSprites != null)
            {
                foreach (var sp in playerSprites) sp.enabled = true;
            }
            DoTeleport();
        }
    }

    private void OnVideoFinished(UnityEngine.Video.VideoPlayer vp)
    {
        MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoEnded(vp);
        PlayerPrefs.SetInt("JustUsedBoat", 1);
        PlayerPrefs.Save();
        Debug.Log("[Boat] Chiếu video xong. Chuẩn bị dịch chuyển...");
        DoTeleport();
    }


    private void DoTeleport()
    {
        PlayerPrefs.SetInt("JustUsedBoat", 1);
        PlayerPrefs.Save();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var playerSprites = player.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sp in playerSprites)
            {
                sp.enabled = true;
            }
        }
        if (mapSceneController == null)
        {
            mapSceneController = FindObjectOfType<MapSceneController>();
        }

        if (mapSceneController != null && targetMapData != null)
        {
            if (useSpecificSpawn)
                mapSceneController.EnterMap(targetMapData, false, specificSpawnPosition);
            else
                mapSceneController.EnterMap(targetMapData, false);
        }
        else
        {
            Debug.LogError("[Boat] Lỗi: Chưa gán MapData hoặc không tìm thấy MapSceneController!");
            isTeleportingWithVideo = false;
        }
    }
}
