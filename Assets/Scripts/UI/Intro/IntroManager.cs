using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Collections;
using MysticJourney.Core.Utilities;

public class IntroManager : MonoBehaviour
{
    [Header("Chuyển cảnh")]
    [Tooltip("Scene tiếp theo sẽ load sau khi Intro kết thúc")]
    public string nextSceneName = GameConstants.Scenes.CharacterCreation; // Đổi mặc định thành CharacterCreation
    
    [Header("Tùy chọn Logo (Không dùng video)")]
    public float delayTime = 3f; // Thời gian hiển thị logo (giây)

    [Header("Tùy chọn Video (Ưu tiên)")]
    [Tooltip("Kéo thả Video Player component vào đây. Nếu có VideoPlayer, hệ thống sẽ chờ video chạy xong mới chuyển scene.")]
    public VideoPlayer videoPlayer;

    void Start()
    {
        // Nếu có gắn VideoPlayer thì sẽ ưu tiên chờ Video chạy xong
        if (videoPlayer != null)
        {
            // Bắt sự kiện khi video chạy đến cuối
            videoPlayer.loopPointReached += OnVideoEnd;
        }
        else
        {
            // Nếu không có video, dùng lại cách delay thời gian cũ của logo
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