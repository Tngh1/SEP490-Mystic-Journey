using UnityEngine;
using UnityEngine.UI;

public class VolumeMuteButton : MonoBehaviour
{
    public Slider volumeSlider;

    public GameObject unmuteIcon;
    public GameObject muteIcon;

    // Lưu volume trước khi mute
    private float previousVolume = 1f;

    private void Start()
    {
        volumeSlider.onValueChanged.AddListener(OnSliderChanged);

        UpdateIcon();
    }

    public void ToggleMute()
    {
        // Đang bật tiếng
        if (volumeSlider.value > 0f)
        {
            previousVolume = volumeSlider.value;

            // Mute
            volumeSlider.value = 0f;
        }
        // Đang mute
        else
        {
            // Nếu chưa từng lưu thì mặc định 100%
            if (previousVolume <= 0f)
                previousVolume = 1f;

            // Trả lại volume cũ
            volumeSlider.value = previousVolume;
        }

        UpdateIcon();
    }

    private void OnSliderChanged(float value)
    {
        // Nếu người chơi kéo slider thì cập nhật icon
        if (value > 0f)
        {
            previousVolume = value;
        }

        UpdateIcon();
    }

    private void UpdateIcon()
    {
        bool isMute = volumeSlider.value <= 0.001f;

        unmuteIcon.SetActive(!isMute);
        muteIcon.SetActive(isMute);
    }
}