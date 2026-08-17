using System;
using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MysticJourney.UI;

namespace MysticJourney.Screen.Login
{
    // Executes core business logic for mono behaviour.
    // Logic details: validates required non-empty string arguments.
    public class LoginUIManager : MonoBehaviour
    {
        [Header("Input Fields (TMP)")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("Buttons")]
        [SerializeField] private Button loginButton;

        [Header("Remember Me")]
        [SerializeField] private Toggle rememberMeToggle;

        [Header("Scene Flow (chỉ chuyển khi login thành công)")]
        [Tooltip("Thời gian chờ (giây) trước khi chuyển scene, để user kịp đọc log.")]
        [SerializeField, Min(0f)] private float delayBeforeSceneLoad = 0.5f;

        [Header("Failed Popup UI")]
        [SerializeField] private GameObject failedPopup;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private Button popupExitButton;

        public event System.Action<LoginGameResponse> OnLoginSuccess;
        public event System.Action<ApiException> OnLoginFailed;

        private bool _isLoggingIn;

        // Initializes internal component caches and dependencies for LoginUIManager upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            var pendingReason = MysticJourney.Core.Services.SessionService.PendingLogoutReason;
            if (!string.IsNullOrEmpty(pendingReason))
            {
                gameObject.SetActive(true);

                // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                StartCoroutine(ShowLogoutNotificationDelayed(pendingReason));
            }
        }

        // Performs startup initialization for LoginUIManager on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
            if (passwordInput != null)
                passwordInput.contentType = TMP_InputField.ContentType.Password;

            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginButtonClicked);

            if (failedPopup != null)
                failedPopup.SetActive(false);

            if (popupExitButton != null)
                popupExitButton.onClick.AddListener(CloseFailedPopup);

            LoadRememberMeData();
        }

        // Executes core business logic for show logout notification delayed.
        // Logic details: validates required non-empty string arguments.
        private IEnumerator ShowLogoutNotificationDelayed(string reason)
        {
            yield return null;
            ShowLogoutNotification(reason);
        }

        // Executes core business logic for show logout notification.
        // Logic details: validates required non-empty string arguments.
        private void ShowLogoutNotification(string reason)
        {
            string title = "Logged Out";
            string message = reason;

            if (string.IsNullOrEmpty(message))
            {
                message = "Your session has ended. Please log in again.";
            }

            bool popupShown = TryShowUIPopupBox(title, message);

            if (!popupShown && failedPopup != null)
            {
                ShowErrorPopup(message);
            }

            Debug.Log($"[LoginUIManager] Logout notification shown: {reason}");
            MysticJourney.Core.Services.SessionService.ClearPendingLogoutReason();
        }

        // Executes core business logic for try show ui popup box.
        // Returns a boolean indicating operation success.
        private bool TryShowUIPopupBox(string title, string message)
        {
            try
            {
                return UIPopupBox.Notify(transform, title, message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LoginUIManager] Failed to show UIPopupBox: {ex.Message}");
                return false;
            }
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDestroy()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(OnLoginButtonClicked);

            if (popupExitButton != null)
                popupExitButton.onClick.RemoveListener(CloseFailedPopup);

            if (rememberMeToggle != null)
                rememberMeToggle.onValueChanged.RemoveListener(OnRememberMeChanged);
        }


        // Executes core business logic for on login button clicked.
        // Logic details: validates required non-empty string arguments.
        public void OnLoginButtonClicked()
        {
            if (_isLoggingIn) return;

            string emailOrUser = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
            string password = passwordInput != null ? passwordInput.text : string.Empty;

            if (string.IsNullOrEmpty(emailOrUser) || string.IsNullOrEmpty(password))
            {
                ShowErrorPopup("Please enter your Username/Email and password.");
                return;
            }

            _isLoggingIn = true;
            SetInteractable(false);

            AuthApi.Instance.LoginGame(
                emailOrUser,
                password,
                response =>
                {
                    _isLoggingIn = false;
                    SetInteractable(true);

                    if (failedPopup != null) failedPopup.SetActive(false);

                    Debug.Log("========== [LoginUIManager] LOGIN OK ==========");
                    Debug.Log($"  UserName        : {response.UserName}");
                    Debug.Log($"  AccountId       : {response.AccountId}");
                    Debug.Log("================================================");

                    WorldState.HasCharacter = response.HasCharacter;
                    WorldState.PlayerProfileId = response.PlayerProfileId ?? 0;
                    WorldState.PlayerName = response.PlayerDisplayName ?? response.UserName;
                    WorldState.PlayerClass = response.PlayerClass;
                    WorldState.PlayerLevel = response.Level;
                    if (!string.IsNullOrEmpty(response.LastMapName))
                    {
                        WorldState.CurrentMapName = response.LastMapName;
                        WorldState.LastPosition = new UnityEngine.Vector3((float)response.PositionX, (float)response.PositionY, 0f);
                    }
                    WorldState.SaveToPlayerPrefs();

                    OnLoginSuccess?.Invoke(response);

                    SaveRememberMeData(response.UserName);

                    if (!response.HasCharacter)
                    {
                        Debug.Log("[LoginUIManager] Loading Intro1Scene for new player...");
                        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                        StartCoroutine(LoadSceneAfterDelay("Intro1Scene", delayBeforeSceneLoad));
                    }
                    else
                    {
                        Debug.Log($"[LoginUIManager] Loading game via Bootstrap...");
                        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                        StartCoroutine(LoadSceneAfterDelay(MysticJourney.Core.Utilities.GameConstants.Scenes.Bootstrap, delayBeforeSceneLoad));
                    }
                },
                error =>
                {
                    _isLoggingIn = false;
                    SetInteractable(true);

                    ShowErrorPopup(!string.IsNullOrEmpty(error.Message) ? error.Message : "Login failed. Please try again.");

                    Debug.LogError("========== [LoginUIManager] LOGIN FAIL ==========");
                    Debug.LogError($"  StatusCode : {error.StatusCode}");
                    Debug.LogError($"  Message    : {error.Message}");
                    Debug.LogError("=================================================");

                    OnLoginFailed?.Invoke(error);
                }
            );
        }

        // Executes core business logic for show error popup.
        private void ShowErrorPopup(string message)
        {
            if (failedPopup == null || errorText == null) return;

            errorText.text = message;
            failedPopup.SetActive(true);

            failedPopup.transform.SetAsLastSibling();
        }

        // Executes core business logic for close failed popup.
        public void CloseFailedPopup()
        {
            if (failedPopup != null)
                failedPopup.SetActive(false);
        }


        // Executes core business logic for load scene after delay.
        private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[LoginUIManager] Scene '{sceneName}' chưa có trong Build Settings → KHÔNG chuyển.");
                yield break;
            }

            var placeholderCamGO = new GameObject("__TransitionCamera__");
            var placeholderCam   = placeholderCamGO.AddComponent<Camera>();
            placeholderCam.clearFlags       = CameraClearFlags.SolidColor;
            placeholderCam.backgroundColor  = Color.black;
            placeholderCam.depth            = -100;
            DontDestroyOnLoad(placeholderCamGO);

            SceneManager.LoadScene(sceneName);
        }

        // Executes core business logic for set interactable.
        // Logic details: validates required non-empty string arguments.
        private void SetInteractable(bool interactable)
        {
            if (loginButton != null) loginButton.interactable = interactable;
            if (usernameInput != null) usernameInput.interactable = interactable;
            if (passwordInput != null) passwordInput.interactable = interactable;
        }

        // Executes core business logic for truncate.
        // Logic details: validates required non-empty string arguments.
        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }


        // Executes core business logic for load remember me data.
        // Logic details: validates required non-empty string arguments.
        private void LoadRememberMeData()
        {
            if (rememberMeToggle == null)
                return;

            rememberMeToggle.onValueChanged.AddListener(OnRememberMeChanged);

            bool rememberMe = PlayerPrefs.GetInt(ApiConfig.RememberMeKey, 0) == 1;
            rememberMeToggle.isOn = rememberMe;

            if (rememberMe)
            {
                string savedUsername = PlayerPrefs.GetString(ApiConfig.SavedUsernameKey, string.Empty);
                if (!string.IsNullOrEmpty(savedUsername) && usernameInput != null)
                {
                    usernameInput.text = savedUsername;
                }
            }
        }

        // Executes core business logic for on remember me changed.
        private void OnRememberMeChanged(bool isOn)
        {
            PlayerPrefs.SetInt(ApiConfig.RememberMeKey, isOn ? 1 : 0);

            if (!isOn && usernameInput != null)
            {
                PlayerPrefs.DeleteKey(ApiConfig.SavedUsernameKey);
            }
        }

        // Executes core business logic for save remember me data.
        // Logic details: validates required non-empty string arguments.
        private void SaveRememberMeData(string username)
        {
            if (rememberMeToggle != null && rememberMeToggle.isOn && !string.IsNullOrEmpty(username))
            {
                PlayerPrefs.SetString(ApiConfig.SavedUsernameKey, username);
            }
            PlayerPrefs.Save();
        }
    }
}
