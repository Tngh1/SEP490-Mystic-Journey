using UnityEngine;

public class MenuUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject startPanel;
    public GameObject loginPanel;
    public GameObject websitePanel;

    [Header("Website")]
    public string websiteUrl = "http://localhost:3000/";
    //public string websiteUrl = "http://localhost:3000/";

    private void Start()
    {
        startPanel.SetActive(true);
        loginPanel.SetActive(false);
        websitePanel.SetActive(true);   // hiện nút Website
    }

    // Login
    public void OpenLoginPanel()
    {
        startPanel.SetActive(false);
        loginPanel.SetActive(true);
    }

    // Register
    public void OpenRegisterWebsite()
    {
        Application.OpenURL(websiteUrl);
    }

    // Website Button
    public void OpenWebsite()
    {
        Application.OpenURL(websiteUrl);
    }

    // Back từ LoginPanel
    public void BackToStart()
    {
        startPanel.SetActive(true);
        loginPanel.SetActive(false);
    }
}