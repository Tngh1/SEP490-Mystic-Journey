using System.Collections;
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

        [Header("Tabs System")]
        [SerializeField] private Button mainTabButton;          // Gán MainButton vào đây
        [SerializeField] private Button controllerTabButton;    // Gán ControlButton vào đây
        [SerializeField] private GameObject audioAndGraphicPage;// Gán AudioAndGraphicPage vào đây
        [SerializeField] private GameObject controllerPage;     // Gán ControllerPage vào đây
        [SerializeField] private ControlRebindManager controlRebindManager;

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
        [SerializeField] private TextMeshProUGUI popupMainText;   // Đã thêm: Gán Object "MainText" vào đây
        [SerializeField] private Button popupOkButton;        // Nút "OK" trên popup (Lưu và thoát)
        [SerializeField] private Button popupCancelButton;    // Nút "X" trên popup (Hủy thoát, ở lại cài đặt)

        private SettingState savedState;

        private void Start()
        {
            ForceInitialize();

            // Gán sự kiện cho các nút Main Panel
            if (saveChangeButton != null) saveChangeButton.onClick.AddListener(OnSaveChangeClicked);
            if (settingsExitButton != null) settingsExitButton.onClick.AddListener(OnSettingsExitClicked);

            // Gán sự kiện cho các nút Popup
            if (popupOkButton != null) popupOkButton.onClick.AddListener(OnPopupOkClicked);
            if (popupCancelButton != null) popupCancelButton.onClick.AddListener(OnPopupCancelClicked);

            // Gán sự kiện cho hệ thống Tabs chuyển trang
            if (mainTabButton != null) mainTabButton.onClick.AddListener(() => SwitchToPage(true));
            if (controllerTabButton != null) controllerTabButton.onClick.AddListener(() => SwitchToPage(false));

            LoadCurrentSettings();

            // Subscribe event trùng phím từ ControlRebindManager
            if (controlRebindManager != null)
                controlRebindManager.OnConflictDetected += ShowConflictPopup;

            // Mặc định ban đầu luôn mở trang Audio & Graphic trước
            SwitchToPage(true);
        }

        public void ForceInitialize()
        {
            // Tự động tìm Popup nếu chưa gán (hỗ trợ cả tên ConfirmSettingPopup và SettingPopup như trong hình)
            if (confirmPanel == null)
            {
                var popups = Resources.FindObjectsOfTypeAll<RectTransform>();
                foreach (var p in popups)
                {
                    if ((p.name == "ConfirmSettingPopup" || p.name == "SettingPopup") && p.gameObject.scene.IsValid() && !string.IsNullOrEmpty(p.gameObject.scene.name))
                    {
                        confirmPanel = p.gameObject;
                        break;
                    }
                }
            }

            if (confirmPanel != null)
            {
                confirmPanel.SetActive(false);

                // Tự động tìm Nút
                if (popupOkButton == null || popupCancelButton == null)
                {
                    var buttons = confirmPanel.GetComponentsInChildren<Button>(true);
                    foreach (var btn in buttons)
                    {
                        if (popupOkButton == null && (btn.name == "BtnYes" || btn.name == "YesButton" || btn.name == "BtnOK" || btn.name == "OKButton"))
                            popupOkButton = btn;
                        else if (popupCancelButton == null && (btn.name == "BtnNo" || btn.name == "NoButton" || btn.name == "BtnCancel" || btn.name == "ExitButton"))
                            popupCancelButton = btn;
                    }
                }

                // Tự động tìm MainText nếu chưa gán
                if (popupMainText == null)
                {
                    var texts = confirmPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var txt in texts)
                    {
                        if (txt.name == "MainText")
                        {
                            popupMainText = txt;
                            break;
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (saveChangeButton != null) saveChangeButton.onClick.RemoveListener(OnSaveChangeClicked);
            if (settingsExitButton != null) settingsExitButton.onClick.RemoveListener(OnSettingsExitClicked);

            if (popupOkButton != null) popupOkButton.onClick.RemoveListener(OnPopupOkClicked);
            if (popupCancelButton != null) popupCancelButton.onClick.RemoveListener(OnPopupCancelClicked);

            if (mainTabButton != null) mainTabButton.onClick.RemoveAllListeners();
            if (controllerTabButton != null) controllerTabButton.onClick.RemoveAllListeners();

            // Unsubscribe event trùng phím
            if (controlRebindManager != null)
                controlRebindManager.OnConflictDetected -= ShowConflictPopup;
        }

        /// <summary>Hiện popup lỗi trùng phím (không có nút OK) trong 2 giây rồi tự đóng.</summary>
        private void ShowConflictPopup(string message)
        {
            if (confirmPanel == null) return;

            if (popupMainText != null) popupMainText.text = message;

            // Ẩn nút OK — đây là popup thông báo lỗi, không cần hành động
            if (popupOkButton != null) popupOkButton.gameObject.SetActive(false);

            StopCoroutine(nameof(HideConflictPopupAfterDelay));
            StartCoroutine(nameof(HideConflictPopupAfterDelay));
            confirmPanel.SetActive(true);
        }

        private System.Collections.IEnumerator HideConflictPopupAfterDelay()
        {
            yield return new WaitForSecondsRealtime(2f);

            if (confirmPanel != null) confirmPanel.SetActive(false);

            // Khôi phục nút OK cho lần dùng popup thông thường
            if (popupOkButton != null) popupOkButton.gameObject.SetActive(true);
        }


        // --- HÀM CHUYỂN ĐỔI TAB TRANG ---
        private void SwitchToPage(bool isMainPage)
        {
            if (audioAndGraphicPage != null) audioAndGraphicPage.SetActive(isMainPage);
            if (controllerPage != null) controllerPage.SetActive(!isMainPage);

            if (mainTabButton != null) mainTabButton.interactable = !isMainPage;
            if (controllerTabButton != null) controllerTabButton.interactable = isMainPage;
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

            if (controlRebindManager != null)
                controlRebindManager.LoadBindings();
        }

        // --- HÀM HIỂN THỊ POPUP ĐỘNG ---

        /// <summary>
        /// Gọi hàm này để hiển thị Popup với bất kỳ thông báo nào bạn muốn
        /// </summary>
        public void ShowConfirmPopup(string message)
        {
            if (confirmPanel != null)
            {
                if (popupMainText != null)
                {
                    popupMainText.text = message;
                }
                confirmPanel.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[GameSettingUIManager] Không tìm thấy Confirm Panel để hiển thị lỗi: {message}");
            }
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
            ApiClient.Instance.ClearToken();
            Debug.Log("[Settings] Đã đăng xuất, quay về màn hình Login.");
            SceneManager.LoadScene("LoginScene");
        }

        private void OnSettingsExitClicked()
        {
            if (HasUnsavedChanges())
            {
                // Truyền chuỗi thông báo vào hàm mới tạo
                ShowConfirmPopup("You have unsaved changes. Do you want to apply them before exiting?");
                return;
            }

            CloseSettingsPanel();
        }

        // --- SỰ KIỆN POPUP ---

        private void OnPopupOkClicked()
        {
            SaveSettings();
            savedState = CaptureCurrentState();

            if (confirmPanel != null) confirmPanel.SetActive(false);
            CloseSettingsPanel();
        }

        private void OnPopupCancelClicked()
        {
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
            if (controlRebindManager != null)
            {
                controlRebindManager.SaveBindings();
            }
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

            bool audioGraphicChanged =
                !Mathf.Approximately(current.MasterVolume, savedState.MasterVolume) ||
                !Mathf.Approximately(current.MusicVolume, savedState.MusicVolume) ||
                !Mathf.Approximately(current.SfxVolume, savedState.SfxVolume) ||
                current.MuteAll != savedState.MuteAll ||
                current.DamageNumbers != savedState.DamageNumbers ||
                current.DisplayMode != savedState.DisplayMode ||
                current.Resolution != savedState.Resolution;

            bool bindingChanged = controlRebindManager != null && controlRebindManager.HasUnsavedChanges;

            return audioGraphicChanged || bindingChanged;
        }
    }
}