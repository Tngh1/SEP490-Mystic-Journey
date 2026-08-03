using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binder for a single row in the Gacha rates/detail list (Assets/Prefab/UI/GachaDetailItem.prefab).
/// The manager sets fields explicitly so text never lands in the wrong TMP.
/// </summary>
public class GachaDetailItemUI : MonoBehaviour
{
    [Header("--- GachaDetailItem Binder ---")]
    public TextMeshProUGUI typeText;      // rarity name (Legendary, Epic, ...)
    public Image rarityIconImage;         // rarity gem icon (GachaCommon...GachaMythic)
    public TextMeshProUGUI itemNameText;
    public Image itemIconImage;           // the item's own icon
    public TextMeshProUGUI rateText;      // drop rate, e.g. "20%"
    public Image backgroundImage;         // row background
}
