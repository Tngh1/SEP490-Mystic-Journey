using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MysticJourney.API.Core;

namespace MysticJourney.Screen.GameSetting
{
    // Executes core business logic for mono behaviour.
    public class GameSettingUIManager : MonoBehaviour
    {
        // Executes core business logic for setting state.
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
        [SerializeField] private Button mainTabButton;
        [SerializeField] private Button controllerTabButton;
        [SerializeField] private GameObject audioAndGraphicPage;
        [SerializeField] private GameObject controllerPage;
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
        [SerializeField] private Button saveChangeButton;
        [SerializeField] private Button settingsExitButton;

        [Header("Confirm Popup")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TextMeshProUGUI popupMainText;
        [SerializeField] private Button popupOkButton;
        [SerializeField] private Button popupCancelButton;

        private SettingState savedState;

        private static readonly Vector2Int[] SupportedResolutions =
        {
            new(1280, 720),
            new(1600, 900),
            new(1920, 1080)
        };

        private List<Resolution> filteredResolutions;

        // Performs startup initialization for GameSettingUIManager on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
            ForceInitialize();

            InitGraphicsDropdowns();

            if (saveChangeButton != null) saveChangeButton.onClick.AddListener(OnSaveChangeClicked);
            if (settingsExitButton != null) settingsExitButton.onClick.AddListener(OnSettingsExitClicked);

            if (popupOkButton != null) popupOkButton.onClick.AddListener(OnPopupOkClicked);
            if (popupCancelButton != null) popupCancelButton.onClick.AddListener(OnPopupCancelClicked);

            if (mainTabButton != null) mainTabButton.onClick.AddListener(() => SwitchToPage(true));
            if (controllerTabButton != null) controllerTabButton.onClick.AddListener(() => SwitchToPage(false));

            LoadCurrentSettings();

            if (controlRebindManager != null)
                controlRebindManager.OnConflictDetected += ShowConflictPopup;

            SwitchToPage(true);
        }

        // Executes core business logic for force initialize.
        // Logic details: validates required non-empty string arguments.
        public void ForceInitialize()
        {
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

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDestroy()
        {
            if (saveChangeButton != null) saveChangeButton.onClick.RemoveListener(OnSaveChangeClicked);
            if (settingsExitButton != null) settingsExitButton.onClick.RemoveListener(OnSettingsExitClicked);

            if (popupOkButton != null) popupOkButton.onClick.RemoveListener(OnPopupOkClicked);
            if (popupCancelButton != null) popupCancelButton.onClick.RemoveListener(OnPopupCancelClicked);

            if (mainTabButton != null) mainTabButton.onClick.RemoveAllListeners();
            if (controllerTabButton != null) controllerTabButton.onClick.RemoveAllListeners();

            if (controlRebindManager != null)
                controlRebindManager.OnConflictDetected -= ShowConflictPopup;
        }


        // Executes core business logic for init graphics dropdowns.
        private void InitGraphicsDropdowns()
        {
            if (displayModeDropdown != null)
            {
                displayModeDropdown.ClearOptions();
                displayModeDropdown.AddOptions(new List<string>
                {
                    "Fullscreen",
                    "Borderless Window",
                    "Windowed"
                });
            }

            if (resolutionDropdown != null)
            {
                filteredResolutions = new List<Resolution>();
                resolutionDropdown.ClearOptions();

                Resolution[] availableResolutions = UnityEngine.Screen.resolutions;
                foreach (Vector2Int target in SupportedResolutions)
                {
                    if (TryGetBestSupportedResolution(availableResolutions, target, out Resolution resolution))
                        filteredResolutions.Add(resolution);
                }

                if (filteredResolutions.Count == 0)
                {
                    foreach (Vector2Int target in SupportedResolutions)
                    {
                        filteredResolutions.Add(new Resolution
                        {
                            width = target.x,
                            height = target.y
                        });
                    }
                }

                var options = new List<string>(filteredResolutions.Count);
                foreach (Resolution resolution in filteredResolutions)
                    options.Add($"{resolution.width} x {resolution.height}");

                resolutionDropdown.AddOptions(options);
                resolutionDropdown.RefreshShownValue();
            }
        }

        // Attempt get best supported resolution using available resolutions, target, and best match; it guards invalid or unavailable states and processes each matching entry.
        private static bool TryGetBestSupportedResolution(
            Resolution[] availableResolutions,
            Vector2Int target,
            out Resolution bestMatch)
        {
            bestMatch = default;
            bool found = false;
            double bestRefreshRate = double.MinValue;

            foreach (Resolution resolution in availableResolutions)
            {
                if (resolution.width != target.x || resolution.height != target.y)
                    continue;

                double refreshRate = resolution.refreshRateRatio.value;
                if (double.IsNaN(refreshRate))
                    refreshRate = 0d;

                if (!found || refreshRate > bestRefreshRate)
                {
                    bestMatch = resolution;
                    bestRefreshRate = refreshRate;
                    found = true;
                }
            }

            return found;
        }

        // Executes core business logic for apply graphics settings.
        private void ApplyGraphicsSettings(int resolutionIndex, int displayModeIndex)
        {
            if (filteredResolutions == null || filteredResolutions.Count == 0) return;

            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            int safeResIndex = Mathf.Clamp(resolutionIndex, 0, filteredResolutions.Count - 1);
            Resolution res = filteredResolutions[safeResIndex];

            FullScreenMode mode = FullScreenMode.ExclusiveFullScreen;
            switch (displayModeIndex)
            {
                case 0: mode = FullScreenMode.ExclusiveFullScreen; break;
                case 1: mode = FullScreenMode.FullScreenWindow; break;
                case 2: mode = FullScreenMode.Windowed; break;
            }

            UnityEngine.Screen.SetResolution(res.width, res.height, mode);
            Debug.Log($"[Graphics] Applied: {res.width}x{res.height} - Mode: {mode}");
        }

        // Executes core business logic for switch to page.
        private void SwitchToPage(bool isMainPage)
        {
            if (audioAndGraphicPage != null) audioAndGraphicPage.SetActive(isMainPage);
            if (controllerPage != null) controllerPage.SetActive(!isMainPage);

            if (mainTabButton != null) mainTabButton.interactable = !isMainPage;
            if (controllerTabButton != null) controllerTabButton.interactable = isMainPage;
        }

        // Executes core business logic for load current settings.
        private void LoadCurrentSettings()
        {
            var settings = SettingsService.Instance;
            settings.Load();

            if (masterVolumeSlider != null) masterVolumeSlider.value = settings.MasterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = settings.MusicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = settings.SfxVolume;
            if (muteAllToggle != null) muteAllToggle.SetState(settings.IsMuted);

            if (!settings.HasSessionGraphicsSettings)
            {
                settings.InitializeSessionGraphics(
                    GetCurrentDisplayModeIndex(),
                    FindInitialResolutionIndex());
            }

            if (displayModeDropdown != null)
            {
                // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
                displayModeDropdown.value = Mathf.Clamp(
                    settings.DisplayModeIndex,
                    0,
                    displayModeDropdown.options.Count - 1);
                displayModeDropdown.RefreshShownValue();
            }

            if (resolutionDropdown != null && filteredResolutions != null && filteredResolutions.Count > 0)
            {
                // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
                int safeIndex = Mathf.Clamp(settings.ResolutionIndex, 0, filteredResolutions.Count - 1);
                resolutionDropdown.value = safeIndex;
                resolutionDropdown.RefreshShownValue();
            }

            if (damageNumbersToggle != null) damageNumbersToggle.SetState(settings.ShowDamageNumbers);

            savedState = CaptureCurrentState();

            if (controlRebindManager != null)
                controlRebindManager.LoadBindings();
        }

        // Executes core business logic for find initial resolution index.
        private int FindInitialResolutionIndex()
        {
            if (filteredResolutions == null || filteredResolutions.Count == 0)
                return 0;

            int exactIndex = filteredResolutions.FindIndex(
                resolution => resolution.width == UnityEngine.Screen.width &&
                              resolution.height == UnityEngine.Screen.height);
            if (exactIndex >= 0)
                return exactIndex;

            int fullHdIndex = filteredResolutions.FindIndex(
                resolution => resolution.width == 1920 && resolution.height == 1080);
            return fullHdIndex >= 0 ? fullHdIndex : filteredResolutions.Count - 1;
        }

        // Executes core business logic for get current display mode index.
        private static int GetCurrentDisplayModeIndex()
        {
            return UnityEngine.Screen.fullScreenMode switch
            {
                FullScreenMode.ExclusiveFullScreen => 0,
                FullScreenMode.FullScreenWindow => 1,
                _ => 2
            };
        }


        // Executes core business logic for on save change clicked.
        public void OnSaveChangeClicked()
        {
            SaveSettings();
            savedState = CaptureCurrentState();
            Debug.Log("[GameSettingUIManager] Settings Saved.");
        }

        // Executes core business logic for on logout clicked.
        public void OnLogoutClicked()
        {
            UIPopupBox.Show(
                transform,
                "Logout",
                $"Logout from {UnityEngine.PlayerPrefs.GetString("UserName", "your account")}?",
                () => MysticJourney.Core.Services.SessionService.Logout()
            );
        }

        // Executes core business logic for on settings exit clicked.
        private void OnSettingsExitClicked()
        {
            if (HasUnsavedChanges())
            {
                ShowConfirmPopup("You have unsaved changes. Do you want to apply them before exiting?");
                return;
            }

            CloseSettingsPanel();
        }


        // Executes core business logic for on popup ok clicked.
        private void OnPopupOkClicked()
        {
            SaveSettings();
            savedState = CaptureCurrentState();

            if (confirmPanel != null) confirmPanel.SetActive(false);
            CloseSettingsPanel();
        }

        // Executes core business logic for on popup cancel clicked.
        private void OnPopupCancelClicked()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            Debug.Log("[GameSettingUIManager] Cancelled exit. Staying in settings.");
        }

        // Executes core business logic for show conflict popup.
        private void ShowConflictPopup(string message)
        {
            if (confirmPanel == null) return;

            if (popupMainText != null) popupMainText.text = message;

            if (popupOkButton != null) popupOkButton.gameObject.SetActive(false);

            StopCoroutine(nameof(HideConflictPopupAfterDelay));
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(nameof(HideConflictPopupAfterDelay));
            confirmPanel.SetActive(true);
        }

        // Executes core business logic for hide conflict popup after delay.
        private IEnumerator HideConflictPopupAfterDelay()
        {
            yield return new WaitForSecondsRealtime(2f);

            if (confirmPanel != null) confirmPanel.SetActive(false);

            if (popupOkButton != null) popupOkButton.gameObject.SetActive(true);
        }

        // Executes core business logic for show confirm popup.
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


        // Executes core business logic for save settings.
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

            int resIndex = resolutionDropdown != null ? resolutionDropdown.value : 0;
            int displayIndex = displayModeDropdown != null ? displayModeDropdown.value : 0;
            ApplyGraphicsSettings(resIndex, displayIndex);

            if (controlRebindManager != null)
            {
                controlRebindManager.SaveBindings();
            }
        }

        // Update visibility for settings panel; it updates active.
        private void CloseSettingsPanel()
        {
            gameObject.SetActive(false);
        }

        // Executes core business logic for capture current state.
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

        // Executes core business logic for has unsaved changes.
        // Returns a boolean indicating operation success.
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
