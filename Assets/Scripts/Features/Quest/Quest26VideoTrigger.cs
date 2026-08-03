using UnityEngine;
using UnityEngine.Video;
using MysticJourney.Features.Quest;

public class Quest26VideoTrigger : MonoBehaviour
{
    private VideoPlayer _videoPlayer;
    private bool _hasSubscribed;
    private bool _hasPlayed;
    private bool _initialStateChecked;
    private bool _wasAlreadyFinishedOnLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (FindObjectOfType<Quest26VideoTrigger>() != null) return;
        var go = new GameObject("Quest26VideoTrigger");
        DontDestroyOnLoad(go);
        go.AddComponent<Quest26VideoTrigger>();
    }

    private void Update()
    {
        if (!_hasSubscribed && QuestManager.Instance != null)
        {
            SubscribeToQuestManager();
        }

        CheckQuestStatus();
    }

    private void SubscribeToQuestManager()
    {
        if (QuestManager.Instance == null) return;
        _hasSubscribed = true;
        QuestManager.Instance.OnQuestProgressChanged += OnQuestChanged;
        QuestManager.Instance.OnQuestClaimed += OnQuestClaimed;
        QuestManager.Instance.OnQuestsLoaded += OnQuestsLoaded;
        CheckInitialState();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestProgressChanged -= OnQuestChanged;
            QuestManager.Instance.OnQuestClaimed -= OnQuestClaimed;
            QuestManager.Instance.OnQuestsLoaded -= OnQuestsLoaded;
        }
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }

    private void CheckInitialState()
    {
        if (_initialStateChecked || QuestManager.Instance == null) return;
        var state = QuestManager.Instance.GetQuestState(26);
        if (state != null)
        {
            _initialStateChecked = true;
            // If it was already Claimed when logging in, mark as finished on load so we don't replay on game start
            if (state.status == "Claimed")
            {
                _wasAlreadyFinishedOnLoad = true;
            }
        }
    }

    private void OnQuestsLoaded()
    {
        CheckInitialState();
        CheckQuestStatus();
    }

    private void OnQuestChanged(int questId)
    {
        if (questId == 26 || questId == -1)
        {
            CheckQuestStatus();
        }
    }

    private void OnQuestClaimed(int questId)
    {
        if (questId == 26)
        {
            CheckQuestStatus();
        }
    }

    private void CheckQuestStatus()
    {
        if (_hasPlayed || _wasAlreadyFinishedOnLoad || QuestManager.Instance == null) return;

        var state = QuestManager.Instance.GetQuestState(26);
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
