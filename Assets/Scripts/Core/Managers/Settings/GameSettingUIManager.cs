using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MysticJourney.Screen.GameSetting
{
    public class GameSettingUIManager : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private ToggleButtonUI muteAllToggle;

        [Header("Graphic")]
        [SerializeField] private TMP_Dropdown displayModeDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private ToggleButtonUI damageNumbersToggle;

        [Header("Buttons")]
        [SerializeField] private Button saveChangeButton;

        private void Start()
        {
            if (saveChangeButton != null)
                saveChangeButton.onClick.AddListener(OnSaveChangeClicked);

            LoadCurrentSettings();
        }

        private void OnDestroy()
        {
            if (saveChangeButton != null)
                saveChangeButton.onClick.RemoveListener(OnSaveChangeClicked);
        }

        private void LoadCurrentSettings()
        {
            var settings = SettingsService.Instance;
            settings.Load();

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
                damageNumbersToggle.SetState(settings.ShowDamageNumbers);
        }

        public void OnSaveChangeClicked()
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
            Debug.Log("[GameSettingUIManager] Settings saved.");
        }

        private string GetDropdownText(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options.Count == 0) return string.Empty;
            return dropdown.options[dropdown.value].text;
        }
    }
}
