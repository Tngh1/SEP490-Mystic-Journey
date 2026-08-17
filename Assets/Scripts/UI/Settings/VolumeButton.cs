
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.Core.Utilities;

// Executes mono behaviour operation.
public class VolumeMuteButton : MonoBehaviour
{
    public Slider volumeSlider;
    public GameObject unmuteIcon;
    public GameObject muteIcon;

    // Executes volume type operation.
    public enum VolumeType { Master, Music, SFX }
    public VolumeType volumeType = VolumeType.Master;

    private float _previousVolume = 1f;

    // Performs startup initialization for VolumeMuteButton on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        volumeSlider.onValueChanged.AddListener(OnSliderChanged);
        UpdateIcon();
    }

    // Executes toggle mute operation.
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
        ApplyVolumeChange(volumeSlider.value);
    }

    // Executes on slider changed operation.
    private void OnSliderChanged(float value)
    {
        if (value > GameConstants.Timing.MuteThreshold)
            _previousVolume = value;

        UpdateIcon();
        ApplyVolumeChange(value);
    }

    // Executes apply volume change operation.
    private void ApplyVolumeChange(float val)
    {
        if (volumeType == VolumeType.Master) SettingsService.Instance.SetMasterVolume(val);
        else if (volumeType == VolumeType.Music) SettingsService.Instance.SetMusicVolume(val);
        else if (volumeType == VolumeType.SFX) SettingsService.Instance.SetSfxVolume(val);
    }

    // Executes update icon operation.
    private void UpdateIcon()
    {
        bool isMute = volumeSlider.value <= GameConstants.Timing.MuteThreshold;

        if (unmuteIcon != null) unmuteIcon.SetActive(!isMute);
        if (muteIcon != null) muteIcon.SetActive(isMute);
    }
}
