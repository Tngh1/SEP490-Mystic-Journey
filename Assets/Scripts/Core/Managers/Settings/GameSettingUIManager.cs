using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MysticJourney.API.Core;

namespace MysticJourney.Screen.GameSetting
{
    public class GameSettingUIManager : MonoBehaviour
    {
        [System.Serializable]
        private class SettingState
        {
            public float MasterVolume;
            public float MusicVolume;
            public float SfxVolume;

            public bool MuteAll;
            public bool DamageNumbers;

            public int DisplayMode;
            public int Resolution;
        }

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private ToggleButtonUI muteAllToggle;

        [Header("Graphic")]
        [SerializeField] private TMP_Dropdown displayModeDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private ToggleButtonUI damageNumbersToggle;

        [Header("Main Panel Buttons")]
        [SerializeField] private Button saveChangeButton;     // Nút "Save Change" dưới quyển sách
        [SerializeField] private Button settingsExitButton;   // Nút "X" góc trên phải của quyển sách

        [Header("Confirm Popup")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Button popupOkButton;        // Nút "OK" trên popup (Lưu và thoát)
        [SerializeField] private Button popupCancelButton;    // Nút "X" trên popup (Hủy thoát, ở lại cài đặt)

        private SettingState savedState;

        private void Start()
        {
            if (confirmPanel != null)
                confirmPanel.SetActive(false);

            // Gán sự kiện cho các nút Main Panel
            if (saveChangeButton != null) saveChangeButton.onClick.AddListener(OnSaveChangeClicked);
            if (settingsExitButton != null) settingsExitButton.onClick.AddListener(OnSettingsExitClicked);

            // Gán sự kiện cho các nút Popup
            if (popupOkButton != null) popupOkButton.onClick.AddListener(OnPopupOkClicked);
            if (popupCancelButton != null) popupCancelButton.onClick.AddListener(OnPopupCancelClicked);

            LoadCurrentSettings();
        }

        private void OnDestroy()
        {
            if (saveChangeButton != null) saveChangeButton.onClick.RemoveListener(OnSaveChangeClicked);
            if (settingsExitButton != null) settingsExitButton.onClick.RemoveListener(OnSettingsExitClicked);

            if (popupOkButton != null) popupOkButton.onClick.RemoveListener(OnPopupOkClicked);
            if (popupCancelButton != null) popupCancelButton.onClick.RemoveListener(OnPopupCancelClicked);
        }

        private void LoadCurrentSettings()
        {
            var settings = SettingsService.Instance;
            settings.Load();

            if (masterVolumeSlider != null) masterVolumeSlider.value = settings.MasterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = settings.MusicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = settings.SfxVolume;
            if (muteAllToggle != null) muteAllToggle.SetState(settings.IsMuted);
            if (displayModeDropdown != null) displayModeDropdown.value = settings.DisplayModeIndex;
            if (resolutionDropdown != null) resolutionDropdown.value = settings.ResolutionIndex;
            if (damageNumbersToggle != null) damageNumbersToggle.SetState(settings.ShowDamageNumbers);

            savedState = CaptureCurrentState();
        }

        // --- SỰ KIỆN MAIN PANEL ---

        public void OnSaveChangeClicked()
        {
            SaveSettings();
            savedState = CaptureCurrentState();
            Debug.Log("[GameSettingUIManager] Settings Saved.");
        }

        public void OnLogoutClicked()
        {
            // 1. Xóa toàn bộ dữ liệu phiên (Token, ID, v.v.)
            ApiClient.Instance.ClearToken();

            Debug.Log("[Settings] Đã đăng xuất, quay về màn hình Login.");

            // 2. Load lại Scene Login
            // Thay "LoginScene" bằng tên Scene đăng nhập thực tế của bạn
            SceneManager.LoadScene("LoginScene");
        }

        private void OnSettingsExitClicked()
        {
            // Nếu có thay đổi chưa lưu -> Bật Popup hỏi
            if (HasUnsavedChanges())
            {
                if (confirmPanel != null)
                    confirmPanel.SetActive(true);

                return;
            }

            // Nếu không có thay đổi gì -> Tắt bảng cài đặt luôn
            CloseSettingsPanel();
        }

        // --- SỰ KIỆN POPUP ---

        private void OnPopupOkClicked()
        {
            // Người dùng chọn OK -> Lưu lại -> Tắt popup -> Đóng bảng cài đặt
            SaveSettings();
            savedState = CaptureCurrentState();

            if (confirmPanel != null) confirmPanel.SetActive(false);
            CloseSettingsPanel();
        }

        private void OnPopupCancelClicked()
        {
            // Người dùng chọn X trên popup -> Chỉ tắt popup để quay lại chỉnh sửa tiếp
            if (confirmPanel != null) confirmPanel.SetActive(false);
            Debug.Log("[GameSettingUIManager] Cancelled exit. Staying in settings.");
        }

        // --- HÀM HỖ TRỢ ---

        private void SaveSettings()
        {
            var settings = SettingsService.Instance;

            if (masterVolumeSlider != null) settings.SetMasterVolume(masterVolumeSlider.value);
            if (musicVolumeSlider != null) settings.SetMusicVolume(musicVolumeSlider.value);
            if (sfxVolumeSlider != null) settings.SetSfxVolume(sfxVolumeSlider.value);
            if (muteAllToggle != null) settings.SetMuted(muteAllToggle.isOn);
            if (displayModeDropdown != null) settings.SetDisplayMode(displayModeDropdown.value);
            if (resolutionDropdown != null) settings.SetResolution(resolutionDropdown.value);
            if (damageNumbersToggle != null) settings.SetShowDamageNumbers(damageNumbersToggle.isOn);

            settings.Save();
        }

        private void CloseSettingsPanel()
        {
            gameObject.SetActive(false);
        }

        private SettingState CaptureCurrentState()
        {
            return new SettingState
            {
                MasterVolume = masterVolumeSlider != null ? masterVolumeSlider.value : 0f,
                MusicVolume = musicVolumeSlider != null ? musicVolumeSlider.value : 0f,
                SfxVolume = sfxVolumeSlider != null ? sfxVolumeSlider.value : 0f,
                MuteAll = muteAllToggle != null && muteAllToggle.isOn,
                DamageNumbers = damageNumbersToggle != null && damageNumbersToggle.isOn,
                DisplayMode = displayModeDropdown != null ? displayModeDropdown.value : 0,
                Resolution = resolutionDropdown != null ? resolutionDropdown.value : 0
            };
        }

        private bool HasUnsavedChanges()
        {
            if (savedState == null) return false;

            SettingState current = CaptureCurrentState();

            return
                !Mathf.Approximately(current.MasterVolume, savedState.MasterVolume) ||
                !Mathf.Approximately(current.MusicVolume, savedState.MusicVolume) ||
                !Mathf.Approximately(current.SfxVolume, savedState.SfxVolume) ||
                current.MuteAll != savedState.MuteAll ||
                current.DamageNumbers != savedState.DamageNumbers ||
                current.DisplayMode != savedState.DisplayMode ||
                current.Resolution != savedState.Resolution;
        }
    }
}