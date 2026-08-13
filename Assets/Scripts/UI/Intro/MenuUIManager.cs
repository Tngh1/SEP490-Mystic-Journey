using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Button exitGameButton;

    [Header("Website")]
    public string websiteUrl = "http://localhost:3000/";
    //public string websiteUrl = "http://localhost:3000/";

    private void Awake()
    {
        if (exitGameButton == null)
        {
            Transform exitTransform = transform.Find("ExitGameButton") ?? transform.Find("ExitGame");
            if (exitTransform != null)
                exitGameButton = exitTransform.GetComponent<Button>();
        }

        AddHoverEffect(loginButton);
        AddHoverEffect(registerButton);
        AddHoverEffect(websiteButton);
        AddHoverEffect(exitButton);
        AddHoverEffect(exitGameButton != null ? exitGameButton.gameObject : null);

        if (exitGameButton != null)
        {
            exitGameButton.onClick.RemoveListener(ExitGame);
            exitGameButton.onClick.AddListener(ExitGame);
        }
    }

    private void OnDestroy()
    {
        if (exitGameButton != null)
            exitGameButton.onClick.RemoveListener(ExitGame);
    }

    private static void AddHoverEffect(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<UIHoverScaleEffect>() == null)
            go.AddComponent<UIHoverScaleEffect>();
    }

    private void Start()
    {
        bool hasLogoutReason = !string.IsNullOrEmpty(MysticJourney.Core.Services.SessionService.PendingLogoutReason);

        if (hasLogoutReason)
        {
            if (startPanel != null) startPanel.SetActive(false);
            if (loginPanel != null) loginPanel.SetActive(true);
        }
        else
        {
            if (startPanel != null) startPanel.SetActive(true);
            if (loginPanel != null) loginPanel.SetActive(false);
        }

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
            Application.OpenURL(websiteUrl.TrimEnd('/') + "/register");
    }

    // Website Button
    public void OpenWebsite()
    {
        if (!string.IsNullOrEmpty(websiteUrl))
            Application.OpenURL(websiteUrl);
    }

    public void ExitGame()
    {
        Debug.Log("[MenuUIManager] Exiting game...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Back từ LoginPanel
    public void BackToStart()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);
    }
}
