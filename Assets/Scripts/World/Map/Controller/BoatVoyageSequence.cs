using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Chạy trình tự "đi thuyền sang map khác": loading -> dịch chuyển -> chiếu video.
///
/// Vì sao phải là một object RIÊNG và DontDestroyOnLoad:
/// VideoPlayer của thuyền nằm TRÊN CÙNG GameObject với thuyền, tức thuộc scene AutumnPumpkin.
/// MapSceneController.ChangeMap unload scene cũ, nên thuyền + VideoPlayer + coroutine đang chạy
/// trên nó đều bị huỷ giữa đường. Muốn video chiếu SAU khi đã sang map mới thì phải có một
/// VideoPlayer sống sót qua lần unload đó.
/// </summary>
public class BoatVoyageSequence : MonoBehaviour
{
    // Chờ ChangeMap xong. Quá mốc này coi như EnterMap thất bại (sai config, thiếu scene trong
    // Build Settings...) -> gỡ loading để người chơi không bị kẹt sau màn hình loading vĩnh viễn.
    private const float TeleportTimeoutSeconds = 30f;
    // Video treo (không phát, không bắn loopPointReached) thì vẫn phải trả màn hình lại cho người chơi.
    private const float VideoWatchdogGraceSeconds = 5f;

    // Chốt cuối: nếu qua mốc này mà video vẫn chưa bắt đầu và trình tự cũng chưa xong thì coi như
    // Run() đã chết (exception trong coroutine bị Unity log rồi dừng lặng lẽ) -> cưỡng chế gỡ loading.
    // Phải LỚN HƠN TeleportTimeoutSeconds để không cắt ngang lần dịch chuyển đang chạy bình thường.
    private const float WatchdogSeconds = TeleportTimeoutSeconds + 10f;

    private static BoatVoyageSequence _active;

    private VideoPlayer _videoPlayer;
    private bool _videoFinished;
    private bool _videoStarted;
    private bool _finished;

    // Coroutine của LoadingScreen.Show đang chạy song song. Phải chờ nó xong TRƯỚC khi Hide:
    // Hide() unload theo tên scene và bỏ qua nếu scene chưa isLoaded, nên Hide chạy trước lúc
    // LoadSceneAsync kịp xong sẽ không unload gì — rồi scene loading load xong và ở lại mãi.
    private Coroutine _showRoutine;

    // Luôn dùng hàm này thay cho LoadingScreen.Hide() trực tiếp.
    private IEnumerator HideLoading()
    {
        if (_showRoutine != null)
            yield return _showRoutine;

        yield return LoadingScreen.Hide();
    }

    /// <summary>
    /// Bắt đầu chuyến đi. Gọi từ thuyền lúc bấm E; thuyền có thể bị huỷ ngay sau đó cũng không sao
    /// vì mọi thứ cần dùng đã được copy sang đây.
    /// </summary>
    public static void Begin(
        MapSceneController mapSceneController,
        MapData targetMapData,
        Vector3? specificSpawnPosition,
        VideoPlayer sourceVideoPlayer)
    {
        if (_active != null)
            return;

        if (mapSceneController == null || targetMapData == null)
        {
            Debug.LogError("[BoatVoyage] Thiếu MapSceneController hoặc MapData — không dịch chuyển được.");
            return;
        }

        var host = new GameObject("[BoatVoyageSequence]");
        DontDestroyOnLoad(host);

        _active = host.AddComponent<BoatVoyageSequence>();
        _active.StartCoroutine(_active.Run(mapSceneController, targetMapData, specificSpawnPosition, sourceVideoPlayer));
        // Watchdog chạy SONG SONG, là coroutine riêng: nếu Run() chết giữa đường thì nó vẫn sống để
        // gỡ màn hình loading. Cùng nằm trên coroutine của Run thì chết chung, vô nghĩa.
        _active.StartCoroutine(_active.WatchdogUnstick());
    }

    /// <summary>
    /// Lưới an toàn cuối cùng cho case "treo Sailing...": exception trong coroutine chỉ khiến Unity
    /// log rồi dừng coroutine đó, KHÔNG ai gỡ LoadingScreen -> người chơi kẹt vĩnh viễn sau màn hình
    /// loading, phải tắt game. Watchdog này đảm bảo luôn có người gỡ loading.
    /// </summary>
    private IEnumerator WatchdogUnstick()
    {
        var deadline = Time.unscaledTime + WatchdogSeconds;
        while (Time.unscaledTime < deadline)
        {
            // Trình tự đã xong, hoặc video đã bắt đầu chiếu (từ đây WaitForVideoEnd tự có watchdog
            // riêng) -> không cần can thiệp.
            if (_finished || _videoStarted)
                yield break;
            yield return null;
        }

        Debug.LogError("[BoatVoyage] Trình tự không hoàn tất đúng cách (có thể Run() đã lỗi) — cưỡng chế gỡ loading.");
        // CỐ Ý gọi thẳng LoadingScreen.Hide(), KHÔNG qua HideLoading(): nếu thứ đang treo lại chính
        // là _showRoutine thì chờ nó sẽ treo luôn cả watchdog — đúng cái mà watchdog phải chống.
        yield return LoadingScreen.Hide();
        Finish();
    }

    private IEnumerator Run(
        MapSceneController mapSceneController,
        MapData targetMapData,
        Vector3? specificSpawnPosition,
        VideoPlayer sourceVideoPlayer)
    {
        WorldInteractionPromptRuntime.Hide();

        // 1. Chụp lại CLIP (và audio mode) NGAY BÂY GIỜ, lúc thuyền còn sống.
        //    sourceVideoPlayer là component nằm trên GameObject thuyền, tức thuộc scene cũ. Sau khi
        //    ChangeMap unload scene đó, component bị huỷ và mọi lần đọc sourceVideoPlayer.clip sẽ ném
        //    MissingReferenceException. VideoClip là ASSET nên sống độc lập với scene -> giữ asset,
        //    không giữ component.
        VideoClip clip = null;
        var audioMode = VideoAudioOutputMode.Direct;
        if (sourceVideoPlayer != null)
        {
            clip = sourceVideoPlayer.clip;
            audioMode = sourceVideoPlayer.audioOutputMode == VideoAudioOutputMode.None
                ? VideoAudioOutputMode.None
                : VideoAudioOutputMode.Direct;
        }

        // 2. Bật loading nhưng KHÔNG chờ nó xong (StartCoroutine, không yield return).
        //
        // Đây chính là chỗ treo "Sailing...": LoadingScreen.Show() gọi
        // LoadingProgress.Report(0.05f, "Sailing...") TRƯỚC rồi mới
        // `yield return SceneManager.LoadSceneAsync("Loading", Additive)`. Nên nếu lần load scene
        // Loading đó không hoàn tất (đang có lần load/unload khác chồng lên), coroutine đứng im tại
        // đúng dòng đó: chữ "Sailing..." đã hiện, EnterMap chưa từng được gọi, và KHÔNG có exception
        // nào để thấy trong Console — khớp đúng hiện tượng.
        //
        // Màn hình loading chỉ là thứ trang trí; dịch chuyển mới là việc bắt buộc. Tách ra chạy song
        // song thì Show có chậm/treo cũng không cản được bước dịch chuyển bên dưới.
        _showRoutine = StartCoroutine(LoadingScreen.Show("Sailing..."));

        // 3. Dịch chuyển NGAY, TRƯỚC mọi việc liên quan tới video.
        //
        // Trước đây PrepareVideo() chạy ở đây (giữa Show và EnterMap) để tranh thủ giải mã clip
        // song song với lúc load map. Nhưng nếu nó ném exception thì coroutine chết ngay tại chỗ:
        // EnterMap không bao giờ được gọi, LoadingScreen không bao giờ được Hide -> treo vĩnh viễn ở
        // đúng chữ "Sailing..." (ChangeMap mà chạy thì đã đổi text thành "Loading map..."). Dịch
        // chuyển là việc BẮT BUỘC phải xong, video chỉ là hiệu ứng -> làm việc bắt buộc trước.
        var fromMap = WorldState.CurrentMapName;
        if (specificSpawnPosition.HasValue)
            mapSceneController.EnterMap(targetMapData, false, specificSpawnPosition.Value);
        else
            mapSceneController.EnterMap(targetMapData, false);

        // 4. Chờ tới khi ĐÃ sang map mới. ChangeMap gán WorldState.CurrentMapName sau khi scene mới
        //    load xong, nên đây là mốc "đã dịch chuyển" mà không cần biết tên scene đích.
        var deadline = Time.unscaledTime + TeleportTimeoutSeconds;
        while (WorldState.CurrentMapName == fromMap && Time.unscaledTime < deadline)
            yield return null;

        if (WorldState.CurrentMapName == fromMap)
        {
            Debug.LogError($"[BoatVoyage] Quá {TeleportTimeoutSeconds}s vẫn chưa sang được {targetMapData.mapName}. Gỡ loading.");
            yield return HideLoading();
            Finish();
            yield break;
        }

        // 5. Đã sang map mới an toàn -> giờ mới dựng VideoPlayer. Bọc try/catch: dựng video lỗi thì
        //    chỉ là mất đoạn phim, KHÔNG được kéo theo màn hình loading treo mãi như trước.
        //    Prepare() không block (async), nên gọi ở đây không làm chậm gì.
        try
        {
            PrepareVideo(clip, audioMode);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BoatVoyage] Không dựng được VideoPlayer, bỏ qua video: {ex}");
            _videoPlayer = null;
        }

        // Không có clip (thuyền chưa gán, hoặc vừa dựng lỗi) -> chỉ loading rồi vào map.
        if (_videoPlayer == null || _videoPlayer.clip == null)
        {
            yield return HideLoading();
            Finish();
            yield break;
        }

        // 6. Dựng overlay video (đen, sortingOrder 32767) NGAY khi vừa sang map, TRƯỚC cả khi chờ
        //    clip prepared. Lý do: ChangeMap tự gọi LoadingScreen.Hide() ngay sau khi gán
        //    CurrentMapName, nên nếu chờ prepared xong mới bật overlay thì có một khoảng màn hình
        //    loading đã tắt mà video chưa che -> người chơi thấy map mới nháy ra rồi mới vào video.
        //    Overlay lên trước thì dù loading tắt bên dưới cũng không lộ gì.
        MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoStarted(_videoPlayer);

        // Từ đây watchdog rút lui: overlay đã che màn hình và WaitForVideoEnd có deadline riêng.
        _videoStarted = true;

        yield return WaitForPrepared();
        _videoPlayer.Play();

        // Loading giờ nằm dưới overlay; gỡ đi cho gọn (ChangeMap có thể đã gỡ sẵn — Hide idempotent).
        yield return HideLoading();

        yield return WaitForVideoEnd();

        MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoEnded(_videoPlayer);
        Finish();
    }

    // Nhận CLIP (asset) thay vì VideoPlayer nguồn: lúc hàm này chạy thì thuyền đã bị unload cùng
    // scene cũ, đọc lại component nguồn sẽ ném MissingReferenceException.
    private void PrepareVideo(VideoClip clip, VideoAudioOutputMode audioMode)
    {
        if (clip == null)
            return;

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = false;
        _videoPlayer.clip = clip;
        _videoPlayer.waitForFirstFrame = true;

        // AudioSource mode cần một AudioSource đi kèm; VideoPlayer gốc nằm ở scene đã bị unload nên
        // không mượn lại được. Direct phát thẳng ra audio output, không phụ thuộc component khác.
        _videoPlayer.audioOutputMode = audioMode;

        _videoPlayer.loopPointReached += OnVideoFinished;
        _videoPlayer.Prepare();
    }

    private IEnumerator WaitForPrepared()
    {
        // Prepare() chạy song song với việc load map nên thường đã xong; mốc này chỉ để chắc chắn
        // không Play() trên clip chưa nạp (nguyên nhân gây khựng frame đầu).
        var deadline = Time.unscaledTime + VideoWatchdogGraceSeconds;
        while (!_videoPlayer.isPrepared && Time.unscaledTime < deadline)
            yield return null;
    }

    private IEnumerator WaitForVideoEnd()
    {
        // Watchdog: nếu video không bao giờ bắn loopPointReached (clip lỗi, decode fail) thì vẫn
        // phải nhả màn hình ra, nếu không người chơi ngồi nhìn overlay đen vĩnh viễn.
        var maxSeconds = (_videoPlayer.length > 0 ? (float)_videoPlayer.length : 0f) + VideoWatchdogGraceSeconds;
        var deadline = Time.unscaledTime + maxSeconds;

        while (!_videoFinished && Time.unscaledTime < deadline)
            yield return null;

        if (!_videoFinished)
            Debug.LogWarning("[BoatVoyage] Video không kết thúc đúng cách — tự tắt overlay.");
    }

    private void OnVideoFinished(VideoPlayer vp) => _videoFinished = true;

    // Idempotent: cả Run() và WatchdogUnstick() đều có thể gọi, và gọi lần hai không được
    // Destroy(gameObject) thêm lần nữa hay xoá _active của một chuyến đi khác.
    private void Finish()
    {
        if (_finished)
            return;
        _finished = true;

        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoFinished;
            _videoPlayer.Stop();
        }

        if (_active == this)
            _active = null;

        Destroy(gameObject);
    }
}
