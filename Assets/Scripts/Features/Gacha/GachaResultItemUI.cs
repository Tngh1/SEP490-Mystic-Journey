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
}
