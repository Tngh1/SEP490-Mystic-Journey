using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binder for a single pulled-item card in the Gacha ResultPopup
/// (Assets/Prefab/Button/GachaItemTemplate.prefab).
/// The manager sets fields explicitly so the rarity frame and the item icon
/// never land on the wrong Image.
/// </summary>
public class GachaResultItemUI : MonoBehaviour
{
    [Header("--- GachaItemTemplate Binder ---")]
    [Tooltip("Khung nền đổi theo độ hiếm (GachaResultCommon ... GachaResultMythic)")]
    public Image typeBgImage;      // child "TypeBg"

    [Tooltip("Icon của vật phẩm quay được")]
    public Image itemIconImage;    // child "Icon"

    [Tooltip("Optional - prefab hiện tại không còn TMP tên vật phẩm")]
    public TextMeshProUGUI itemNameText;

    [Tooltip("Badge 'xN' ở góc dưới phải - chỉ hiện khi quay trùng từ 2 cái trở lên")]
    public TextMeshProUGUI quantityText;   // child "QuantityText"

    /// <summary>
    /// Hiện badge số lượng, tô theo màu độ hiếm của vật phẩm.
    /// count &lt;= 1 thì ẩn hẳn GameObject để card đơn lẻ trông như cũ.
    /// </summary>
    /// <param name="colorHex">Hex màu độ hiếm (vd "#FFD700"). Rỗng thì để nguyên màu trắng của prefab.</param>
    public void SetQuantity(int count, string colorHex)
    {
        if (quantityText == null) return;

        if (count > 1)
        {
            quantityText.text = string.IsNullOrEmpty(colorHex)
                ? $"x{count}"
                : $"<color={colorHex}>x{count}</color>";
            quantityText.gameObject.SetActive(true);
        }
        else
        {
            quantityText.gameObject.SetActive(false);
        }
    }
}
