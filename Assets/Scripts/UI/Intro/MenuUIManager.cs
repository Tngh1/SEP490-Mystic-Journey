using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Executes core business logic for mono behaviour.
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

    [Header("Game Version")]
    [SerializeField] private TMP_Text gameVersionText;

    [Header("Website")]
    public string websiteUrl = "https://mystic-journey.io.vn";

    // Initializes internal component caches and dependencies for MenuUIManager upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (gameVersionText == null)
        {
            Transform versionTransform = transform.Find("GameVersion");
            if (versionTransform != null)
                gameVersionText = versionTransform.GetComponent<TMP_Text>();
        }

        if (gameVersionText != null)
            gameVersionText.text = $"V{Application.version}";

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

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (exitGameButton != null)
            exitGameButton.onClick.RemoveListener(ExitGame);
    }

    // Executes core business logic for add hover effect.
    // Logic details: validates required non-empty string arguments.
    private static void AddHoverEffect(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<UIHoverScaleEffect>() == null)
            go.AddComponent<UIHoverScaleEffect>();
    }

    // Performs startup initialization for MenuUIManager on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
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

    // Executes core business logic for open login panel.
    // Logic details: validates required non-empty string arguments.
    public void OpenLoginPanel()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (loginPanel != null) loginPanel.SetActive(true);
    }

    // Executes core business logic for open register website.
    // Logic details: validates required non-empty string arguments.
    public void OpenRegisterWebsite()
    {
        if (!string.IsNullOrEmpty(websiteUrl))
            Application.OpenURL(websiteUrl.TrimEnd('/') + "/register");
    }

    // Executes core business logic for open website.
    // Logic details: validates required non-empty string arguments.
    public void OpenWebsite()
    {
        if (!string.IsNullOrEmpty(websiteUrl))
            Application.OpenURL(websiteUrl);
    }

    // Executes core business logic for exit game.
    public void ExitGame()
    {
        Debug.Log("[MenuUIManager] Exiting game...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Executes core business logic for back to start.
    public void BackToStart()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);
    }
}
