using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Executes mono behaviour operation.
public class BoatVoyageSequence : MonoBehaviour
{
    private const float TeleportTimeoutSeconds = 30f;
    private const float VideoWatchdogGraceSeconds = 5f;

    private const float WatchdogSeconds = TeleportTimeoutSeconds + 10f;

    private static BoatVoyageSequence _active;

    private VideoPlayer _videoPlayer;
    private bool _videoFinished;
    private bool _videoStarted;
    private bool _finished;

    private Coroutine _showRoutine;

    // Executes hide loading operation.
    private IEnumerator HideLoading()
    {
        if (_showRoutine != null)
            yield return _showRoutine;

        yield return LoadingScreen.Hide();
    }

    // Process begin using map scene controller, target map data, specific spawn position, and source video player; it creates component and starts the timed Unity sequence and guards invalid or unavailable states.
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
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        _active.StartCoroutine(_active.Run(mapSceneController, targetMapData, specificSpawnPosition, sourceVideoPlayer));
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        _active.StartCoroutine(_active.WatchdogUnstick());
    }

    // Executes watchdog unstick operation.
    private IEnumerator WatchdogUnstick()
    {
        var deadline = Time.unscaledTime + WatchdogSeconds;
        while (Time.unscaledTime < deadline)
        {
            if (_finished || _videoStarted)
                yield break;
            yield return null;
        }

        Debug.LogError("[BoatVoyage] Trình tự không hoàn tất đúng cách (có thể Run() đã lỗi) — cưỡng chế gỡ loading.");
        yield return LoadingScreen.Hide();
        Finish();
    }

    // Process run using map scene controller, target map data, specific spawn position, and source video player; it updates navigation or visibility through hide, starts the timed Unity sequence, updates navigation or visibility through show, and updates navigation or visibility through hide loading and guards invalid or unavailable states and translates operation failures.
    private IEnumerator Run(
        MapSceneController mapSceneController,
        MapData targetMapData,
        Vector3? specificSpawnPosition,
        VideoPlayer sourceVideoPlayer)
    {
        WorldInteractionPromptRuntime.Hide();

        VideoClip clip = null;
        var audioMode = VideoAudioOutputMode.Direct;
        if (sourceVideoPlayer != null)
        {
            clip = sourceVideoPlayer.clip;
            audioMode = sourceVideoPlayer.audioOutputMode == VideoAudioOutputMode.None
                ? VideoAudioOutputMode.None
                : VideoAudioOutputMode.Direct;
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        _showRoutine = StartCoroutine(LoadingScreen.Show("Sailing..."));

        var fromMap = WorldState.CurrentMapName;
        if (specificSpawnPosition.HasValue)
            mapSceneController.EnterMap(targetMapData, false, specificSpawnPosition.Value);
        else
            mapSceneController.EnterMap(targetMapData, false);

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

        try
        {
            PrepareVideo(clip, audioMode);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BoatVoyage] Không dựng được VideoPlayer, bỏ qua video: {ex}");
            _videoPlayer = null;
        }

        if (_videoPlayer == null || _videoPlayer.clip == null)
        {
            yield return HideLoading();
            Finish();
            yield break;
        }

        MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoStarted(_videoPlayer);

        _videoStarted = true;

        yield return WaitForPrepared();
        _videoPlayer.Play();

        yield return HideLoading();

        yield return WaitForVideoEnd();

        MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoEnded(_videoPlayer);
        Finish();
    }

    // Executes prepare video operation.
    private void PrepareVideo(VideoClip clip, VideoAudioOutputMode audioMode)
    {
        if (clip == null)
            return;

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = false;
        _videoPlayer.clip = clip;
        _videoPlayer.waitForFirstFrame = true;

        _videoPlayer.audioOutputMode = audioMode;

        _videoPlayer.loopPointReached += OnVideoFinished;
        _videoPlayer.Prepare();
    }

    // Executes wait for prepared operation.
    private IEnumerator WaitForPrepared()
    {
        var deadline = Time.unscaledTime + VideoWatchdogGraceSeconds;
        while (!_videoPlayer.isPrepared && Time.unscaledTime < deadline)
            yield return null;
    }

    // Executes wait for video end operation.
    private IEnumerator WaitForVideoEnd()
    {
        var maxSeconds = (_videoPlayer.length > 0 ? (float)_videoPlayer.length : 0f) + VideoWatchdogGraceSeconds;
        var deadline = Time.unscaledTime + maxSeconds;

        while (!_videoFinished && Time.unscaledTime < deadline)
            yield return null;

        if (!_videoFinished)
            Debug.LogWarning("[BoatVoyage] Video không kết thúc đúng cách — tự tắt overlay.");
    }

    // Executes on video finished operation.
    private void OnVideoFinished(VideoPlayer vp) => _videoFinished = true;

    // Executes finish operation.
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
