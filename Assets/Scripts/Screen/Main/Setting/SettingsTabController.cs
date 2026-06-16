using UnityEngine;
using UnityEngine.UI;

public class SettingsTabController : MonoBehaviour
{
    [Header("Pages")]
    public GameObject audioPage;
    public GameObject videoPage;
    public GameObject controlsPage;

    [Header("Tab Images")]
    public Image audioTab;
    public Image videoTab;
    public Image controlsTab;

    [Header("Sprites")]
    public Sprite selectedSprite;
    public Sprite normalSprite;

    private void Start()
    {
        ShowAudio();
    }

    public void ShowAudio()
    {
        audioPage.SetActive(true);
        videoPage.SetActive(false);
        controlsPage.SetActive(false);

        UpdateTabs(audioTab);
    }

    public void ShowVideo()
    {
        audioPage.SetActive(false);
        videoPage.SetActive(true);
        controlsPage.SetActive(false);

        UpdateTabs(videoTab);
    }

    public void ShowControls()
    {
        audioPage.SetActive(false);
        videoPage.SetActive(false);
        controlsPage.SetActive(true);

        UpdateTabs(controlsTab);
    }

    void UpdateTabs(Image selected)
    {
        audioTab.sprite = normalSprite;
        videoTab.sprite = normalSprite;
        controlsTab.sprite = normalSprite;

        selected.sprite = selectedSprite;
    }
}