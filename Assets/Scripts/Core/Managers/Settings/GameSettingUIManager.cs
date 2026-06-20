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
<<<<<<< HEAD:Assets/Scripts/Core/Managers/Settings/GameSettingUIManager.cs
            var settings = SettingsService.Instance;
            settings.Load();
=======
            // TODO: Sau này load từ API hoặc PlayerPrefs
>>>>>>> 3401475262946c5cd42c446c26436a45745fdb58:Assets/Scripts/Screen/Main/Setting/GameSettingUIManager.cs

            if (masterVolumeSlider != null)
                masterVolumeSlider.value = settings.MasterVolume;
            if (musicVolumeSlider != null)
                musicVolumeSlider.value = settings.MusicVolume;
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = settings.SfxVolume;
            if (muteAllToggle != null)
                muteAllToggle.SetState(settings.IsMuted);
            if (displayModeDropdown != null)
                displayModeDropdown.value = settings.DisplayModeIndex;
            if (resolutionDropdown != null)
                resolutionDropdown.value = settings.ResolutionIndex;
            if (damageNumbersToggle != null)
<<<<<<< HEAD:Assets/Scripts/Core/Managers/Settings/GameSettingUIManager.cs
                damageNumbersToggle.SetState(settings.ShowDamageNumbers);
        }

        public void OnSaveChangeClicked()
=======
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
>>>>>>> 3401475262946c5cd42c446c26436a45745fdb58:Assets/Scripts/Screen/Main/Setting/GameSettingUIManager.cs
        {
            var settings = SettingsService.Instance;

            if (masterVolumeSlider != null) settings.SetMasterVolume(masterVolumeSlider.value);
            if (musicVolumeSlider != null) settings.SetMusicVolume(musicVolumeSlider.value);
            if (sfxVolumeSlider != null) settings.SetSfxVolume(sfxVolumeSlider.value);
            if (muteAllToggle != null) settings.SetMuted(muteAllToggle.isOn);
            if (displayModeDropdown != null) settings.SetDisplayMode(displayModeDropdown.value);
            if (resolutionDropdown != null) settings.SetResolution(resolutionDropdown.value);
            if (damageNumbersToggle != null) settings.SetShowDamageNumbers(damageNumbersToggle.isOn);

<<<<<<< HEAD:Assets/Scripts/Core/Managers/Settings/GameSettingUIManager.cs
            settings.Save();
            Debug.Log("[GameSettingUIManager] Settings saved.");
=======
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
>>>>>>> 3401475262946c5cd42c446c26436a45745fdb58:Assets/Scripts/Screen/Main/Setting/GameSettingUIManager.cs
        }

        private string GetDropdownText(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options.Count == 0) return string.Empty;
            return dropdown.options[dropdown.value].text;
        }
    }
}
