using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binder for a single row in the Gacha history list (Assets/Prefab/UI/GachaItem.prefab).
/// The manager sets fields explicitly so text never lands in the wrong TMP.
/// </summary>
public class GachaHistoryItemUI : MonoBehaviour
{
    [Header("--- GachaHistoryItem Binder ---")]
    public TextMeshProUGUI typeText;      // rarity name (Legendary, Epic, ...)
    public Image rarityIconImage;         // rarity gem icon (GachaCommon...GachaMythic)
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI dateTimeText;
    public Image backgroundImage;         // row background
}
