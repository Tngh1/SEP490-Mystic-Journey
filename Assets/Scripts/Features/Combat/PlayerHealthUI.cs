using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;

    private void OnEnable()
    {
        // Đăng ký "lắng nghe" sự kiện khi UI được bật lên
        PlayerEntity.OnHealthChanged += UpdateHealthUI;
    }

    private void OnDisable()
    {
        // Hủy đăng ký khi UI bị tắt/xóa (Tránh lỗi văng game rò rỉ bộ nhớ)
        PlayerEntity.OnHealthChanged -= UpdateHealthUI;
    }

    // Hàm này sẽ tự động chạy mỗi khi PlayerEntity gọi OnHealthChanged
    private void UpdateHealthUI(int currentHp, int maxHp)
    {
        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = (float)currentHp / maxHp;
        }

        if (hpText != null)
        {
            hpText.text = $"{currentHp} / {maxHp}";
        }
    }
}