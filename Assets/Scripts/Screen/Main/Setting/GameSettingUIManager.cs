using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Main Buttons")]
        [SerializeField] private Button saveChangeButton;
        [SerializeField] private Button settingsCloseButton;

        [Header("Confirm Popup")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Button popupOkButton;
        [SerializeField] private Button popupCloseButton;

        private SettingState savedState;

        private void Start()
        {
            if (confirmPanel != null)
                confirmPanel.SetActive(false);

            saveChangeButton?.onClick.AddListener(OnSaveChangeClicked);
            settingsCloseButton?.onClick.AddListener(OnSettingsCloseClicked);

            popupOkButton?.onClick.AddListener(OnPopupOkClicked);
            popupCloseButton?.onClick.AddListener(OnPopupCloseClicked);

            LoadCurrentSettings();
        }

        private void OnDestroy()
        {
            saveChangeButton?.onClick.RemoveListener(OnSaveChangeClicked);
            settingsCloseButton?.onClick.RemoveListener(OnSettingsCloseClicked);

            popupOkButton?.onClick.RemoveListener(OnPopupOkClicked);
            popupCloseButton?.onClick.RemoveListener(OnPopupCloseClicked);
        }

        private void LoadCurrentSettings()
        {
            // TODO: Sau này load từ API hoặc PlayerPrefs

            if (masterVolumeSlider != null)
                masterVolumeSlider.value = 1f;

            if (musicVolumeSlider != null)
                musicVolumeSlider.value = 1f;

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = 1f;

            if (muteAllToggle != null)
                muteAllToggle.isOn = false;

            if (displayModeDropdown != null)
                displayModeDropdown.value = 0;

            if (resolutionDropdown != null)
                resolutionDropdown.value = 0;

            if (damageNumbersToggle != null)
                damageNumbersToggle.isOn = true;

            savedState = CaptureCurrentState();
        }

        private void OnSaveChangeClicked()
        {
            SaveSettings();

            savedState = CaptureCurrentState();

            Debug.Log("[GameSettingUIManager] Settings Saved.");
        }

        private void OnSettingsCloseClicked()
        {
            if (HasUnsavedChanges())
            {
                if (confirmPanel != null)
                    confirmPanel.SetActive(true);

                return;
            }

            CloseSettingsPanel();
        }

        private void OnPopupOkClicked()
        {
            SaveSettings();

            savedState = CaptureCurrentState();

            if (confirmPanel != null)
                confirmPanel.SetActive(false);

            CloseSettingsPanel();
        }

        private void OnPopupCloseClicked()
        {
            if (confirmPanel != null)
                confirmPanel.SetActive(false);

            CloseSettingsPanel();

            Debug.Log("[GameSettingUIManager] Closed without saving.");
        }

        private void SaveSettings()
        {
            string displayMode = GetDropdownText(displayModeDropdown);
            string resolution = GetDropdownText(resolutionDropdown);

            Debug.Log("========== [GameSettingUIManager] SAVE ==========");

            Debug.Log($"MasterVolume : {masterVolumeSlider?.value ?? 0f}");
            Debug.Log($"MusicVolume  : {musicVolumeSlider?.value ?? 0f}");
            Debug.Log($"SFXVolume    : {sfxVolumeSlider?.value ?? 0f}");
            Debug.Log($"MuteAll      : {muteAllToggle?.isOn}");

            Debug.Log($"DisplayMode  : {displayMode}");
            Debug.Log($"Resolution   : {resolution}");
            Debug.Log($"DamageNumber : {damageNumbersToggle?.isOn}");

            Debug.Log("=================================================");

            // TODO:
            // Call API SaveGameSettings()
            // hoặc PlayerPrefs.Save()
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
            if (savedState == null)
                return false;

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

        private string GetDropdownText(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
                return string.Empty;

            if (dropdown.options.Count == 0)
                return string.Empty;

            return dropdown.options[dropdown.value].text;
        }
    }
}