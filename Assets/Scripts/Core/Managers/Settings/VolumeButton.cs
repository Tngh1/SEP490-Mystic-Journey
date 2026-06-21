using UnityEngine;
using UnityEngine.UI;
using MysticJourney.Core.Utilities;

public class VolumeMuteButton : MonoBehaviour
{
    public Slider volumeSlider;
    public GameObject unmuteIcon;
    public GameObject muteIcon;

    private float _previousVolume = 1f;

    private void Start()
    {
        volumeSlider.onValueChanged.AddListener(OnSliderChanged);
        UpdateIcon();
    }

    public void ToggleMute()
    {
        if (volumeSlider.value > GameConstants.Timing.MuteThreshold)
        {
            _previousVolume = volumeSlider.value;
            volumeSlider.value = 0f;
        }
        else
        {
            if (_previousVolume <= GameConstants.Timing.MuteThreshold)
                _previousVolume = 1f;
            volumeSlider.value = _previousVolume;
        }

        UpdateIcon();
        SettingsService.Instance.SetMasterVolume(volumeSlider.value);
    }

    private void OnSliderChanged(float value)
    {
        if (value > GameConstants.Timing.MuteThreshold)
            _previousVolume = value;

        UpdateIcon();
    }

    private void UpdateIcon()
    {
        bool isMute = volumeSlider.value <= GameConstants.Timing.MuteThreshold;

        if (unmuteIcon != null) unmuteIcon.SetActive(!isMute);
        if (muteIcon != null) muteIcon.SetActive(isMute);
    }
}
