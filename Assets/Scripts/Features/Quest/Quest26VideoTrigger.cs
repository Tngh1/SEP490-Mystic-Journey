using UnityEngine;
using UnityEngine.Video;
using MysticJourney.Features.Quest;

// Executes mono behaviour operation.
public class Quest26VideoTrigger : MonoBehaviour
{
    private VideoPlayer _videoPlayer;
    private bool _hasSubscribed;
    private bool _hasPlayed;
    private bool _initialStateChecked;
    private bool _wasAlreadyFinishedOnLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    // Executes init operation.
    private static void Init()
    {
        if (FindFirstObjectByType<Quest26VideoTrigger>() != null) return;
        var go = new GameObject("Quest26VideoTrigger");
        DontDestroyOnLoad(go);
        go.AddComponent<Quest26VideoTrigger>();
    }

    // Per-frame update loop for Quest26VideoTrigger.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (!_hasSubscribed && QuestUIManager.Instance != null)
        {
            SubscribeToQuestManager();
        }

        CheckQuestStatus();
    }

    // Executes subscribe to quest manager operation.
    private void SubscribeToQuestManager()
    {
        if (QuestUIManager.Instance == null) return;
        _hasSubscribed = true;
        QuestUIManager.Instance.OnQuestProgressChanged += OnQuestChanged;
        QuestUIManager.Instance.OnQuestClaimed += OnQuestClaimed;
        QuestUIManager.Instance.OnQuestsLoaded += OnQuestsLoaded;
        CheckInitialState();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (QuestUIManager.Instance != null)
        {
            QuestUIManager.Instance.OnQuestProgressChanged -= OnQuestChanged;
            QuestUIManager.Instance.OnQuestClaimed -= OnQuestClaimed;
            QuestUIManager.Instance.OnQuestsLoaded -= OnQuestsLoaded;
        }
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }

    // Executes check initial state operation.
    private void CheckInitialState()
    {
        if (_initialStateChecked || QuestUIManager.Instance == null) return;
        var state = QuestUIManager.Instance.GetQuestState(26);
        if (state != null)
        {
            _initialStateChecked = true;
            if (state.status == "Claimed")
            {
                _wasAlreadyFinishedOnLoad = true;
            }
        }
    }

    // Executes on quests loaded operation.
    private void OnQuestsLoaded()
    {
        CheckInitialState();
        CheckQuestStatus();
    }

    // Executes on quest changed operation.
    private void OnQuestChanged(int questId)
    {
        if (questId == 26 || questId == -1)
        {
            CheckQuestStatus();
        }
    }

    // Executes on quest claimed operation.
    private void OnQuestClaimed(int questId)
    {
        if (questId == 26)
        {
            CheckQuestStatus();
        }
    }

    // Executes check quest status operation.
    private void CheckQuestStatus()
    {
        if (_hasPlayed || _wasAlreadyFinishedOnLoad || QuestUIManager.Instance == null) return;

        var state = QuestUIManager.Instance.GetQuestState(26);
        if (state == null) return;

        int target = state.targetAmount > 0 ? state.targetAmount : 2;
        bool isFinished = state.progress >= target || state.status == "Completed" || state.status == "Claimed";

        if (isFinished)
        {
            _hasPlayed = true;
            Debug.Log($"[Quest26VideoTrigger] Quest 26 completed! Progress={state.progress}/{target}, Status={state.status}. Playing video!");
            PlayVideo();
        }
    }

    // Executes play video operation.
    private void PlayVideo()
    {
        if (_videoPlayer == null)
        {
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
        }

        VideoClip clip = Resources.Load<VideoClip>("EndMapTuyet");
        if (clip != null)
        {
            _videoPlayer.clip = clip;
            _videoPlayer.loopPointReached += OnVideoFinished;

            QuestVideoManager.NotifyVideoStarted(_videoPlayer);
            _videoPlayer.gameObject.SetActive(true);
            _videoPlayer.Play();
            Debug.Log("[Quest26VideoTrigger] EndMapTuyet video started playing.");
        }
        else
        {
            Debug.LogError("[Quest26VideoTrigger] Could not load EndMapTuyet video clip from Resources!");
        }
    }

    // Executes on video finished operation.
    private void OnVideoFinished(VideoPlayer vp)
    {
        if (vp == _videoPlayer)
        {
            vp.Stop();
            vp.gameObject.SetActive(false);
            QuestVideoManager.NotifyVideoEnded(vp);
            Debug.Log("[Quest26VideoTrigger] EndMapTuyet video finished.");
        }
    }
}
