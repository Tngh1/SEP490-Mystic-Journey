using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class IntroManager : MonoBehaviour
{
    public float delayTime = 3f; // Thời gian hiển thị logo (giây)
    public string nextSceneName = "MainMenuScene";

    void Start()
    {
        StartCoroutine(LoadNextScene());
    }

    IEnumerator LoadNextScene()
    {
        yield return new WaitForSeconds(delayTime);
        SceneManager.LoadScene(nextSceneName); // Chuyển sang màn hình chờ
    }
}