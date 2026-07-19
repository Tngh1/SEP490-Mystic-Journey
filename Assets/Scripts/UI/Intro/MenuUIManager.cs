using UnityEngine;

public class MenuUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject startPanel;
    public GameObject loginPanel;
    public GameObject websitePanel;

    [Header("Buttons (Hover Effect)")]
    [SerializeField] private GameObject loginButton;
    [SerializeField] private GameObject registerButton;
    [SerializeField] private GameObject websiteButton;
    [SerializeField] private GameObject exitButton;

    [Header("Website")]
    public string websiteUrl = "http://localhost:3000/";
    //public string websiteUrl = "http://localhost:3000/";

    private void Awake()
    {
        AddHoverEffect(loginButton);
        AddHoverEffect(registerButton);
        AddHoverEffect(websiteButton);
        AddHoverEffect(exitButton);
    }

    private static void AddHoverEffect(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<UIHoverScaleEffect>() == null)
            go.AddComponent<UIHoverScaleEffect>();
    }

    private void Start()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (websitePanel != null) websitePanel.SetActive(true);
    }

    // Login
    public void OpenLoginPanel()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (loginPanel != null) loginPanel.SetActive(true);
    }

    // Register
    public void OpenRegisterWebsite()
    {
        if (!string.IsNullOrEmpty(websiteUrl))
            Application.OpenURL(websiteUrl);
    }

    // Website Button
    public void OpenWebsite()
    {
        if (!string.IsNullOrEmpty(websiteUrl))
            Application.OpenURL(websiteUrl);
    }

    // Back từ LoginPanel
    public void BackToStart()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);
    }
}