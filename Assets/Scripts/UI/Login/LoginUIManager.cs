using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MysticJourney.Screen.Login
{
    public class LoginUIManager : MonoBehaviour
    {
        [Header("Input Fields (TMP)")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("Buttons")]
        [SerializeField] private Button loginButton;

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
        }

        private void OnDestroy()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(OnLoginButtonClicked);

            if (popupExitButton != null)
                popupExitButton.onClick.RemoveListener(CloseFailedPopup);
        }

        // ── Click Handler ─────────────────────────────────────────

        public void OnLoginButtonClicked()
        {
            if (_isLoggingIn) return;

            string emailOrUser = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
            string password = passwordInput != null ? passwordInput.text : string.Empty;

            if (string.IsNullOrEmpty(emailOrUser) || string.IsNullOrEmpty(password))
            {
                ShowErrorPopup("Vui lòng nhập Username/Email và mật khẩu.");
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

                    if (string.IsNullOrEmpty(response.PlayerClass))
                    {
                        Debug.Log("[LoginUIManager] Loading IntroScene for new player...");
                        StartCoroutine(LoadSceneAfterDelay("IntroScene", delayBeforeSceneLoad));
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
                    ShowErrorPopup(!string.IsNullOrEmpty(error.Message) ? error.Message : "Đăng nhập thất bại. Vui lòng thử lại!");

                    Debug.LogError("========== [LoginUIManager] LOGIN FAIL ==========");
                    Debug.LogError($"  StatusCode : {error.StatusCode}");
                    Debug.LogError($"  Message    : {error.Message}");
                    Debug.LogError("=================================================");

                    OnLoginFailed?.Invoke(error);
                }
            );
        }

        // ── Failed Popup Helpers ──────────────────────────────────

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
    }
}