
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.Core.Utilities;

public class VolumeMuteButton : MonoBehaviour
{
    public Slider volumeSlider;
    public GameObject unmuteIcon;
    public GameObject muteIcon;
    
    public enum VolumeType { Master, Music, SFX }
    public VolumeType volumeType = VolumeType.Master;

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
        ApplyVolumeChange(volumeSlider.value);
    }

    private void OnSliderChanged(float value)
    {
        if (value > GameConstants.Timing.MuteThreshold)
            _previousVolume = value;

        UpdateIcon();
        ApplyVolumeChange(value);
    }

    private void ApplyVolumeChange(float val)
    {
        if (volumeType == VolumeType.Master) SettingsService.Instance.SetMasterVolume(val);
        else if (volumeType == VolumeType.Music) SettingsService.Instance.SetMusicVolume(val);
        else if (volumeType == VolumeType.SFX) SettingsService.Instance.SetSfxVolume(val);
    }

    private void UpdateIcon()
    {
        bool isMute = volumeSlider.value <= GameConstants.Timing.MuteThreshold;

        if (unmuteIcon != null) unmuteIcon.SetActive(!isMute);
        if (muteIcon != null) muteIcon.SetActive(isMute);
    }
}
