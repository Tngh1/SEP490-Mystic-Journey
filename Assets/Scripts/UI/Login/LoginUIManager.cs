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
    // Controller đơn giản cho màn hình Login trong MainMenuScene.
    // Gắn script này vào GameObject Canvas trong scene MainMenuScene.unity.
    // Kéo UsernameInput, PasswordInput, LoginButton vào Inspector.
    // Điền tên scene muốn chuyển tới sau khi login OK vào field "Scene On Success".
    //
    // Flow:
    //   1. User nhập Email/Username + Password → bấm LoginButton
    //   2. Script gọi AuthApi.Instance.LoginGame()
    //   3. In thông tin response ra Unity Console
    //   4. Nếu login OK + có cấu hình scene → tự chuyển scene sau một khoảng delay
    public class LoginUIManager : MonoBehaviour
    {
        [Header("Input Fields (TMP)")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("Buttons")]
        [SerializeField] private Button loginButton;

        [Header("Scene Flow (chỉ chuyển khi login thành công)")]
        [Tooltip("Tên scene sẽ load sau khi login OK. Để trống nếu muốn script khác tự xử lý.")]
        [SerializeField] private string sceneOnSuccess = "Main";

        [Tooltip("Thời gian chờ (giây) trước khi chuyển scene, để user kịp đọc log.")]
        [SerializeField, Min(0f)] private float delayBeforeSceneLoad = 0.5f;

        // Sự kiện cho script khác lắng nghe (vd: MainMenu chuyển scene khi login OK)
        public event System.Action<LoginGameResponse> OnLoginSuccess;
        public event System.Action<ApiException> OnLoginFailed;

        private bool _isLoggingIn;

        private void Start()
        {
            if (passwordInput != null)
                passwordInput.contentType = TMP_InputField.ContentType.Password;

            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginButtonClicked);
        }

        private void OnDestroy()
        {
            if (loginButton != null)
                loginButton.onClick.RemoveListener(OnLoginButtonClicked);
        }

        // ── Click Handler ─────────────────────────────────────────

        public void OnLoginButtonClicked()
        {
            if (_isLoggingIn) return;

            string emailOrUser = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
            string password = passwordInput != null ? passwordInput.text : string.Empty;

            if (string.IsNullOrEmpty(emailOrUser) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[LoginUIManager] Vui lòng nhập Email/Username và mật khẩu.");
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

                    Debug.Log("========== [LoginUIManager] LOGIN OK ==========");
                    Debug.Log($"  UserName        : {response.UserName}");
                    Debug.Log($"  Email           : {response.EmailAddress}");
                    Debug.Log($"  AccountId       : {response.AccountId}");
                    Debug.Log($"  PlayerProfileId : {response.PlayerProfileId}");
                    Debug.Log($"  DisplayName     : {response.PlayerDisplayName}");
                    Debug.Log($"  RoleId          : {response.RoleId}");
                    Debug.Log($"  AccessToken     : {Truncate(response.AccessToken, 40)}...");
                    Debug.Log($"  AccessExpires   : {response.AccessTokenExpiresAt}");
                    Debug.Log($"  RefreshExpires  : {response.RefreshTokenExpiresAt}");
                    Debug.Log($"  HasToken (sau)  : {ApiClient.Instance.HasToken()}");
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
                        Debug.Log("[LoginUIManager] Account has no character class. Loading CharacterCreation scene...");
                        StartCoroutine(LoadSceneAfterDelay(MysticJourney.Core.Utilities.GameConstants.Scenes.CharacterCreation, delayBeforeSceneLoad));
                    }
                    else
                    {
                        Debug.Log($"[LoginUIManager] Account has character class: {response.PlayerClass}. Loading game via Bootstrap...");
                        StartCoroutine(LoadSceneAfterDelay(MysticJourney.Core.Utilities.GameConstants.Scenes.Bootstrap, delayBeforeSceneLoad));
                    }
                },
                error =>
                {
                    _isLoggingIn = false;
                    SetInteractable(true);

                    Debug.LogError("========== [LoginUIManager] LOGIN FAIL ==========");
                    Debug.LogError($"  StatusCode : {error.StatusCode}");
                    Debug.LogError($"  ErrorCode  : {error.ErrorCode}");
                    Debug.LogError($"  Message    : {error.Message}");
                    Debug.LogError($"  RawBody    : {error.RawBody}");
                    Debug.LogError("=================================================");

                    OnLoginFailed?.Invoke(error);
                }
            );
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

            Debug.Log($"[LoginUIManager] → Loading scene: {sceneName}");
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
