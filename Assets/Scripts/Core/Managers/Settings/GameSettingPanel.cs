using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MysticJourney.Screen.Main.Setting
{
    // Controller cho panel "Game Setting" trong màn hình Main.
    // Cho phép dev / QA chỉnh BaseUrl của backend và xem nhanh các key phiên
    // đang được lưu trong PlayerPrefs (token, accountId, playerProfileId, userName).
    //
    // Cách dùng:
    //   1. Tạo panel GameSetting trong Main scene.
    //   2. Gắn script này vào root của panel.
    //   3. Kéo các TMP_InputField + Button tương ứng vào Inspector.
    //   4. Bấm "Save Change" → các giá trị sẽ được in ra Unity Console.
    //
    // Lưu ý: phiên bản này CHỈ in ra console, KHÔNG ghi đè PlayerPrefs / ApiConfig.
    // (ApiConfig.BaseUrl là const nên cần đổi code nếu muốn áp dụng runtime.)
    public class GameSettingPanel : MonoBehaviour
    {
        [Header("Input Fields (TMP)")]
        [SerializeField] private TMP_InputField baseUrlInput;
        [SerializeField] private TMP_InputField accessTokenInput;
        [SerializeField] private TMP_InputField accountIdInput;
        [SerializeField] private TMP_InputField playerProfileIdInput;
        [SerializeField] private TMP_InputField userNameInput;

        [Header("Buttons")]
        [SerializeField] private Button saveChangeButton;
        [SerializeField] private Button closeButton;

        [Header("Optional: thông báo ngắn trên UI")]
        [SerializeField] private TMP_Text statusText;

        // Key lưu base url mà ApiConfig hiện đang hard-code (chỉ để hiển thị mặc định)
        private const string DefaultBaseUrl = "http://localhost:5176";
        private const string AccessTokenKey = "mj_access_token";
        private const string AccountIdKey = "mj_account_id";
        private const string PlayerProfileIdKey = "mj_player_profile_id";
        private const string UserNameKey = "mj_user_name";

        private void Start()
        {
            if (saveChangeButton != null)
                saveChangeButton.onClick.AddListener(OnSaveChangeClicked);

            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);

            // Khởi tạo giá trị mặc định cho các input để dev thấy được dữ liệu hiện tại
            LoadCurrentValuesToInputs();
        }

        private void OnDestroy()
        {
            if (saveChangeButton != null)
                saveChangeButton.onClick.RemoveListener(OnSaveChangeClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(ClosePanel);
        }

        // ── Click Handlers ────────────────────────────────────────

        public void OnSaveChangeClicked()
        {
            string baseUrl         = baseUrlInput         != null ? baseUrlInput.text.Trim()         : string.Empty;
            string accessToken     = accessTokenInput     != null ? accessTokenInput.text             : string.Empty;
            string accountId       = accountIdInput       != null ? accountIdInput.text.Trim()       : string.Empty;
            string playerProfileId = playerProfileIdInput != null ? playerProfileIdInput.text.Trim() : string.Empty;
            string userName        = userNameInput        != null ? userNameInput.text.Trim()        : string.Empty;

            // In ra console – đúng yêu cầu "chỉ cần hiển thị ra console khi bấm save change"
            Debug.Log("========== [GameSettingPanel] SAVE CHANGE ==========");
            Debug.Log($"  BaseUrl         : {baseUrl}");
            Debug.Log($"  AccessToken     : {Truncate(accessToken, 40)}");
            Debug.Log($"  AccountId       : {accountId}");
            Debug.Log($"  PlayerProfileId : {playerProfileId}");
            Debug.Log($"  UserName        : {userName}");
            Debug.Log("=====================================================");
            Debug.Log($"  → Key lưu trong PlayerPrefs:");
            Debug.Log($"    • {AccessTokenKey}");
            Debug.Log($"    • {AccountIdKey}");
            Debug.Log($"    • {PlayerProfileIdKey}");
            Debug.Log($"    • {UserNameKey}");
            Debug.Log("=====================================================");

            SetStatus("Đã in thông tin ra Console (xem Unity Console).");
        }

        public void ClosePanel()
        {
            gameObject.SetActive(false);
        }

        // ── Helpers ───────────────────────────────────────────────

        private void LoadCurrentValuesToInputs()
        {
            if (baseUrlInput != null)
                baseUrlInput.text = DefaultBaseUrl;

            if (accessTokenInput != null)
            {
                accessTokenInput.text = PlayerPrefs.GetString(AccessTokenKey, string.Empty);
                accessTokenInput.contentType = TMP_InputField.ContentType.Password;
            }

            if (accountIdInput != null)
                accountIdInput.text = PlayerPrefs.HasKey(AccountIdKey)
                    ? PlayerPrefs.GetInt(AccountIdKey).ToString()
                    : string.Empty;

            if (playerProfileIdInput != null)
                playerProfileIdInput.text = PlayerPrefs.HasKey(PlayerProfileIdKey)
                    ? PlayerPrefs.GetInt(PlayerProfileIdKey).ToString()
                    : string.Empty;

            if (userNameInput != null)
                userNameInput.text = PlayerPrefs.GetString(UserNameKey, string.Empty);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
