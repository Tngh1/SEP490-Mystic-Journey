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

        // --- BỔ SUNG BIẾN CHO FAILED POPUP ---
        [Header("Failed Popup UI")]
        [SerializeField] private GameObject failedPopup;     // Object FailedPopup tổng
        [SerializeField] private TMP_Text errorText;         // Chữ hiển thị lỗi
        [SerializeField] private Button popupExitButton;     // Nút X để đóng Popup

        public event System.Action<LoginGameResponse> OnLoginSuccess;
        public event System.Action<ApiException> OnLoginFailed;

        private bool _isLoggingIn;

        private void Awake()
        {
            // Bị buộc logout (session hết hạn/bị đè, mất kết nối) từ scene trước sẽ để lại lý do
            // ở đây. PHẢI check trong Awake (không phải Start) và force active GameObject
            // vì scene có thể load với LoginPanel inactive, Start() sẽ không được gọi.
            var pendingReason = MysticJourney.Core.Services.SessionService.PendingLogoutReason;
            if (!string.IsNullOrEmpty(pendingReason))
            {
                // Force active GameObject để popup hiển thị được
                gameObject.SetActive(true);

                // Trì hoãn 1 frame để đảm bảo scene đã load xong, rồi hiện popup
                StartCoroutine(ShowLogoutNotificationDelayed(pendingReason));
            }
        }

        private void Start()
        {
            if (passwordInput != null)
                passwordInput.contentType = TMP_InputField.ContentType.Password;

            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginButtonClicked);

            // Ẩn popup khi mới bắt đầu game và gán sự kiện cho nút đóng
            if (failedPopup != null)
                failedPopup.SetActive(false);

            if (popupExitButton != null)
                popupExitButton.onClick.AddListener(CloseFailedPopup);

            // Load Remember Me data
            LoadRememberMeData();
        }

        private IEnumerator ShowLogoutNotificationDelayed(string reason)
        {
            yield return null; // Đợi 1 frame
            ShowLogoutNotification(reason);
        }

        /// <summary>
        /// Hiển thị thông báo logout (session hết hạn / bị đè bởi thiết bị khác) 
        /// bằng UIPopupBox trong MainMenu Scene. Message bằng tiếng Anh.
        /// </summary>
        private void ShowLogoutNotification(string reason)
        {
            // Mặc định message bằng tiếng Anh
            string title = "Logged Out";
            string message = reason;

            if (string.IsNullOrEmpty(message))
            {
                message = "Your session has ended. Please log in again.";
            }

            // Thử dùng UIPopupBox trước (popup chuẩn của game)
            // Nếu không tìm thấy UIPopup trong scene, dùng failedPopup có sẵn
            bool popupShown = TryShowUIPopupBox(title, message);

            if (!popupShown && failedPopup != null)
            {
                // Fallback: dùng failedPopup có sẵn trong MainMenuScene
                ShowErrorPopup(message);
            }

            Debug.Log($"[LoginUIManager] Logout notification shown: {reason}");
            MysticJourney.Core.Services.SessionService.ClearPendingLogoutReason();
        }

        /// <summary>
        /// Thử hiển thị bằng UIPopupBox. Trả về true nếu thành công.
        /// </summary>
        private bool TryShowUIPopupBox(string title, string message)
        {
            try
            {
                // UIPopupBox.Notify yêu cầu transform của caller để tìm Canvas
                // Dùng transform của LoginUIManager (thường nằm trong Canvas)
                return UIPopupBox.Notify(transform, title, message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LoginUIManager] Failed to show UIPopupBox: {ex.Message}");
                return false;
            }
        }

        private void OnDestroy()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(OnLoginButtonClicked);

            if (popupExitButton != null)
                popupExitButton.onClick.RemoveListener(CloseFailedPopup);

            if (rememberMeToggle != null)
                rememberMeToggle.onValueChanged.RemoveListener(OnRememberMeChanged);
        }

        // ── Click Handler ─────────────────────────────────────────

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

                    if (failedPopup != null) failedPopup.SetActive(false); // Ẩn popup nếu đang bật

                    Debug.Log("========== [LoginUIManager] LOGIN OK ==========");
                    Debug.Log($"  UserName        : {response.UserName}");
                    Debug.Log($"  AccountId       : {response.AccountId}");
                    Debug.Log("================================================");

                    WorldState.HasCharacter = !string.IsNullOrEmpty(response.PlayerClass);
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

                    // Save username if Remember Me is checked
                    SaveRememberMeData(response.UserName);

                    if (string.IsNullOrEmpty(response.PlayerClass))
                    {
                        Debug.Log("[LoginUIManager] Loading Intro1Scene for new player...");
                        StartCoroutine(LoadSceneAfterDelay("Intro1Scene", delayBeforeSceneLoad));
                    }
                    else
                    {
                        Debug.Log($"[LoginUIManager] Loading game via Bootstrap...");
                        StartCoroutine(LoadSceneAfterDelay(MysticJourney.Core.Utilities.GameConstants.Scenes.Bootstrap, delayBeforeSceneLoad));
                    }
                },
                error =>
                {
                    _isLoggingIn = false;
                    SetInteractable(true);

                    // Hiển thị lỗi lên Popup UI
                    ShowErrorPopup(!string.IsNullOrEmpty(error.Message) ? error.Message : "Login failed. Please try again.");

                    Debug.LogError("========== [LoginUIManager] LOGIN FAIL ==========");
                    Debug.LogError($"  StatusCode : {error.StatusCode}");
                    Debug.LogError($"  Message    : {error.Message}");
                    Debug.LogError("=================================================");

                    OnLoginFailed?.Invoke(error);
                }
            );
        }

        // ── Failed Popup Helpers ──────────────────────────────────

        // Popup này hiện `message` NGUYÊN VĂN, không dịch. Nên mọi nguồn chảy vào đây phải là
        // tiếng Anh: chuỗi hardcode ở file này, `ApiException.Message` do ApiClient tự tạo
        // (SESSION_EXPIRED / PARSE_ERROR), và `message` trong envelope lỗi của BE.
        private void ShowErrorPopup(string message)
        {
            if (failedPopup == null || errorText == null) return;

            errorText.text = message;
            failedPopup.SetActive(true);

            // Đẩy popup lên trên cùng (phòng trường hợp bị che)
            failedPopup.transform.SetAsLastSibling();
        }

        public void CloseFailedPopup()
        {
            if (failedPopup != null)
                failedPopup.SetActive(false);
        }

        // ── Helpers ───────────────────────────────────────────────

        private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[LoginUIManager] Scene '{sceneName}' chưa có trong Build Settings → KHÔNG chuyển.");
                yield break;
            }

            // Tạo camera tạm DontDestroyOnLoad để lấp khoảng trống giữa 2 scene.
            // Nếu không có camera nào tồn tại trong khoảng chuyển scene thì Unity
            // sẽ hiển thị "Display 1 No cameras rendering" trên màn hình.
            // Camera này render solid black (clearFlags = SolidColor, background = black)
            // và sẽ tự mất khi scene mới load xong.
            var placeholderCamGO = new GameObject("__TransitionCamera__");
            var placeholderCam   = placeholderCamGO.AddComponent<Camera>();
            placeholderCam.clearFlags       = CameraClearFlags.SolidColor;
            placeholderCam.backgroundColor  = Color.black;
            placeholderCam.depth            = -100; // Nằm dưới mọi camera thật
            DontDestroyOnLoad(placeholderCamGO);

            SceneManager.LoadScene(sceneName);
        }

        private void SetInteractable(bool interactable)
        {
            if (loginButton != null) loginButton.interactable = interactable;
            if (usernameInput != null) usernameInput.interactable = interactable;
            if (passwordInput != null) passwordInput.interactable = interactable;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        // ── Remember Me Helpers ──────────────────────────────────────

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

        private void OnRememberMeChanged(bool isOn)
        {
            PlayerPrefs.SetInt(ApiConfig.RememberMeKey, isOn ? 1 : 0);

            if (!isOn && usernameInput != null)
            {
                PlayerPrefs.DeleteKey(ApiConfig.SavedUsernameKey);
            }
        }

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