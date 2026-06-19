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

        /// <summary>
        /// Load setting hiện tại lên UI.
        /// Sau này có thể gọi API lấy GameSetting rồi set vào đây.
        /// </summary>
        private void LoadCurrentSettings()
        {
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
        }

        /// <summary>
        /// Click Save Change.
        /// Hiện tại chỉ log ra Console.
        /// Sau này thay bằng gọi API SaveGameSettings().
        /// </summary>
        public void OnSaveChangeClicked()
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