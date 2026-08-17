using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Collections;
using MysticJourney.Core.Utilities;

// Executes core business logic for mono behaviour.
public class IntroManager : MonoBehaviour
{
    [Header("Chuyển cảnh")]
    [Tooltip("Scene tiếp theo sẽ load sau khi Intro kết thúc")]
    public string nextSceneName = GameConstants.Scenes.CharacterCreation;

    [Header("Tùy chọn Logo (Không dùng video)")]
    public float delayTime = 3f;

    [Header("Tùy chọn Video (Ưu tiên)")]
    [Tooltip("Kéo thả Video Player component vào đây. Nếu có VideoPlayer, hệ thống sẽ chờ video chạy xong mới chuyển scene.")]
    public VideoPlayer videoPlayer;

    void Start()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnd;
        }
        else
        {
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(LoadNextSceneWithDelay());
        }
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator LoadNextSceneWithDelay()
    {
        yield return new WaitForSeconds(delayTime);
        SceneManager.LoadScene(nextSceneName);
    }
}
