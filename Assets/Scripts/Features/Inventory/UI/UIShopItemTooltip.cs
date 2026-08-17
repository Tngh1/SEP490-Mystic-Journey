using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class UIShopItemTooltip : MonoBehaviour
{
    // Executes instance operation.
    public static UIShopItemTooltip Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject container;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeRarityText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceLimitText;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Image rarityBorder;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private TMP_SpriteAsset statSpriteAsset;

    // Initializes internal component caches and dependencies for UIShopItemTooltip upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        TryAutoBind();
        EnsureStatIconsAsset();
        DisableRaycastTargets();

        if (container != null) container.SetActive(false);
        else gameObject.SetActive(false);
    }

    // Executes disable raycast targets operation.
    private void DisableRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
    }

    // Executes try auto bind operation.
    private void TryAutoBind()
    {
        if (container == null) container = gameObject;
        if (nameText == null) nameText = FindChildText("NameText", "Name", "Title");
        if (typeRarityText == null) typeRarityText = FindChildText("TypeRarityText", "TypeRarity", "Type", "Rarity");
        if (statsText == null) statsText = FindChildText("StatsText", "Stats", "StatText", "Stat");
        if (descriptionText == null) descriptionText = FindChildText("DescriptionText", "Description", "Desc");
        if (priceLimitText == null) priceLimitText = FindChildText("PriceLimitText", "PriceLimit", "PriceText", "LimitText");

        var allTexts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            var txt = allTexts[i];
            if (txt == null || txt == nameText || txt == typeRarityText) continue;
            if (statsText == null) statsText = txt;
            else if (descriptionText == null && txt != statsText) descriptionText = txt;
            else if (priceLimitText == null && txt != statsText && txt != descriptionText) priceLimitText = txt;
        }

        if (itemIcon == null) itemIcon = FindChildImage("ItemIcon", "Icon");
        if (rarityBorder == null) rarityBorder = FindChildImage("RarityBorder", "Border", "Frame");
    }

    // Executes find child text operation.
    private TMP_Text FindChildText(params string[] names)
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            for (int j = 0; j < names.Length; j++)
            {
                if (texts[i] != null && texts[i].name.Equals(names[j], StringComparison.OrdinalIgnoreCase))
                    return texts[i];
            }
        }
        return null;
    }

    // Executes find child image operation.
    private Image FindChildImage(params string[] names)
    {
        var images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            for (int j = 0; j < names.Length; j++)
            {
                if (images[i] != null && images[i].name.Equals(names[j], StringComparison.OrdinalIgnoreCase))
                    return images[i];
            }
        }
        return null;
    }

    // Executes get or create operation.
    public static UIShopItemTooltip GetOrCreate(Canvas targetCanvas = null)
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<UIShopItemTooltip>(FindObjectsInactive.Include);
        if (Instance != null)
        {
            Instance.gameObject.SetActive(true);
            return Instance;
        }

        Transform parentTransform = null;
        GameObject popupLayerObj = GameObject.Find("PopupLayer");
        if (popupLayerObj != null)
        {
            parentTransform = popupLayerObj.transform;
        }

        Canvas canvas = targetCanvas;
        if (canvas == null && parentTransform != null)
        {
            canvas = parentTransform.GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay || canvases[i].isRootCanvas)
                {
                    canvas = canvases[i];
                    break;
                }
            }
        }

        if (canvas == null && parentTransform == null) return null;
        if (parentTransform == null && canvas != null) parentTransform = canvas.transform;

        GameObject tooltipObj = new GameObject("UIShopItemTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(UIShopItemTooltip));
        tooltipObj.transform.SetParent(parentTransform, false);

        RectTransform rect = tooltipObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 220f);
        rect.pivot = new Vector2(0f, 1f);

        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bgObj.GetComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        GameObject borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;

        Image borderImage = borderObj.GetComponent<Image>();
        borderImage.color = new Color(1f, 0.8f, 0.2f, 0.6f);

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.sizeDelta = new Vector2(-16f, -16f);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = contentObj.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject nameObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(contentObj.transform, false);
        TMP_Text nameTextComp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTextComp.fontSize = 16f;
        nameTextComp.fontStyle = FontStyles.Bold;

        GameObject typeObj = new GameObject("TypeRarityText", typeof(RectTransform), typeof(TextMeshProUGUI));
        typeObj.transform.SetParent(contentObj.transform, false);
        TMP_Text typeTextComp = typeObj.GetComponent<TextMeshProUGUI>();
        typeTextComp.fontSize = 12f;
        typeTextComp.color = new Color(0.7f, 0.7f, 0.8f);

        GameObject statsObj = new GameObject("StatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statsObj.transform.SetParent(contentObj.transform, false);
        TMP_Text statsTextComp = statsObj.GetComponent<TextMeshProUGUI>();
        statsTextComp.fontSize = 13f;
        statsTextComp.richText = true;

        GameObject descObj = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descObj.transform.SetParent(contentObj.transform, false);
        TMP_Text descTextComp = descObj.GetComponent<TextMeshProUGUI>();
        descTextComp.fontSize = 11f;
        descTextComp.fontStyle = FontStyles.Italic;
        descTextComp.color = new Color(0.85f, 0.85f, 0.9f);

        GameObject priceObj = new GameObject("PriceLimitText", typeof(RectTransform), typeof(TextMeshProUGUI));
        priceObj.transform.SetParent(contentObj.transform, false);
        TMP_Text priceTextComp = priceObj.GetComponent<TextMeshProUGUI>();
        priceTextComp.fontSize = 11f;
        priceTextComp.color = new Color(0.9f, 0.75f, 0.3f);

        UIShopItemTooltip script = tooltipObj.GetComponent<UIShopItemTooltip>();
        script.container = tooltipObj;
        script.nameText = nameTextComp;
        script.typeRarityText = typeTextComp;
        script.statsText = statsTextComp;
        script.descriptionText = descTextComp;
        script.priceLimitText = priceTextComp;
        script.rarityBorder = borderImage;

        script.rectTransform = rect;
        script.parentCanvas = canvas;
        script.EnsureStatIconsAsset();

        tooltipObj.SetActive(false);
        Instance = script;
        return script;
    }

    // Executes ensure stat icons asset operation.
    private void EnsureStatIconsAsset()
    {
        if (statSpriteAsset == null)
            statSpriteAsset = Resources.Load<TMP_SpriteAsset>("Sprite Assets/StatIcons");

        if (statSpriteAsset != null && statsText != null)
        {
            statsText.spriteAsset = statSpriteAsset;
        }
    }

    // Executes show tooltip operation.
    public void ShowTooltip(UIItemDisplayData data, RectTransform slotTransform)
    {
        if (data == null)
        {
            HideTooltip();
            return;
        }

        FillFallbackStats(data);
        EnsureStatIconsAsset();

        Color rarityColor = UIItemDetailPopup.GetRarityColor(data.rarity);
        if (nameText != null)
        {
            nameText.enableWordWrapping = true;
            nameText.enableAutoSizing = false;
            nameText.fontSize = 30f;
            nameText.fontStyle = FontStyles.Bold;
            nameText.overflowMode = TextOverflowModes.Overflow;
            nameText.text = data.itemName ?? "Unknown Item";
            nameText.color = rarityColor;
        }

        if (rarityBorder != null)
        {
            rarityBorder.color = rarityColor;
        }

        if (typeRarityText != null)
        {
            typeRarityText.enableAutoSizing = false;
            typeRarityText.enableWordWrapping = true;
            typeRarityText.fontSize = 24f;
            typeRarityText.fontStyle = FontStyles.Bold;
            typeRarityText.overflowMode = TextOverflowModes.Overflow;

            string categoryOrSlot = !string.IsNullOrEmpty(data.slot) && !data.slot.Equals("None", StringComparison.OrdinalIgnoreCase)
                ? data.slot
                : (!string.IsNullOrEmpty(data.category) ? data.category : "Item");
            string rarityStr = !string.IsNullOrEmpty(data.rarity) ? data.rarity : "Common";
            typeRarityText.text = $"{rarityStr} • {categoryOrSlot}";
        }

        if (statsText != null)
        {
            statsText.enableAutoSizing = false;
            statsText.enableWordWrapping = true;
            statsText.fontSize = 24f;
            statsText.fontStyle = FontStyles.Bold;

            StringBuilder sb = new StringBuilder();

            int totalHp = data.baseHp + data.bonusHp;
            int totalAtk = data.baseAtk + data.bonusAtk;
            int totalDef = data.baseDef + data.bonusDef;

            if (totalHp > 0)
                sb.AppendLine($"<sprite name=\"HPStats\"> <color=#B91C1C>HP:</color> <b>+{totalHp}</b>");

            if (totalAtk > 0)
                sb.AppendLine($"<sprite name=\"DMGStats\"> <color=#C2410C>ATK:</color> <b>+{totalAtk}</b>");

            if (totalDef > 0)
                sb.AppendLine($"<sprite name=\"DEFStats\"> <color=#1D4ED8>DEF:</color> <b>+{totalDef}</b>");

            if (data.bonusCritRate > 0f)
                sb.AppendLine($"<sprite name=\"CritStats\"> <color=#15803D>Crit Rate:</color> <b>+{data.bonusCritRate:F1}%</b>");

            if (data.bonusCritDamage > 0f)
                sb.AppendLine($"<sprite name=\"CritDMGStats\"> <color=#6D28D9>Crit Dmg:</color> <b>+{data.bonusCritDamage:F1}%</b>");

            if (data.corruptionReduction > 0f)
                sb.AppendLine($"<color=#B45309>Corruption Reduction:</color> <b>-{data.corruptionReduction:F1}</b>");

            statsText.text = sb.ToString().TrimEnd();
            statsText.gameObject.SetActive(statsText.text.Length > 0);
        }

        if (descriptionText != null)
        {
            descriptionText.enableAutoSizing = false;
            descriptionText.enableWordWrapping = true;
            descriptionText.fontSize = 22f;
            descriptionText.overflowMode = TextOverflowModes.Overflow;

            descriptionText.text = data.description ?? string.Empty;
            descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.description));
        }

        if (priceLimitText != null)
        {
            priceLimitText.enableAutoSizing = false;
            priceLimitText.enableWordWrapping = true;
            priceLimitText.fontSize = 24f;
            priceLimitText.fontStyle = FontStyles.Bold;
            priceLimitText.overflowMode = TextOverflowModes.Overflow;

            StringBuilder sb = new StringBuilder();
            sb.Append($"Price: {data.EffectiveUnitPrice:N0} {data.currency}");

            if (data.dailyPurchaseLimit > 0)
            {
                int remaining = Mathf.Max(0, data.remainingDailyPurchases >= 0 ? data.remainingDailyPurchases : data.dailyPurchaseLimit - data.purchasedToday);
                sb.Append($" (Daily: {remaining}/{data.dailyPurchaseLimit})");
            }
            else if (data.weeklyPurchaseLimit > 0)
            {
                int remaining = Mathf.Max(0, data.remainingWeeklyPurchases >= 0 ? data.remainingWeeklyPurchases : data.weeklyPurchaseLimit - data.purchasedThisWeek);
                sb.Append($" (Weekly: {remaining}/{data.weeklyPurchaseLimit})");
            }

            priceLimitText.text = sb.ToString();
        }

        if (container != null) container.SetActive(true);
        else gameObject.SetActive(true);

        transform.SetAsLastSibling();
        PositionNearSlot(slotTransform);
    }

    // Executes hide tooltip operation.
    public void HideTooltip()
    {
        if (container != null) container.SetActive(false);
        else gameObject.SetActive(false);
    }

    // Executes position near slot operation.
    private void PositionNearSlot(RectTransform slotTransform)
    {
        if (slotTransform == null || rectTransform == null) return;

        Vector3[] corners = new Vector3[4];
        slotTransform.GetWorldCorners(corners);

        Vector3 targetPos = corners[2];
        rectTransform.position = targetPos;

        Vector3[] tooltipCorners = new Vector3[4];
        rectTransform.GetWorldCorners(tooltipCorners);

        float tooltipWidth = Mathf.Abs(tooltipCorners[2].x - tooltipCorners[0].x);
        float tooltipHeight = Mathf.Abs(tooltipCorners[1].y - tooltipCorners[0].y);

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        if (tooltipCorners[2].x > screenWidth - 10f)
        {
            targetPos.x = corners[0].x - tooltipWidth;
        }

        if (tooltipCorners[1].y > screenHeight - 10f)
        {
            targetPos.y -= (tooltipCorners[1].y - (screenHeight - 10f));
        }

        if (tooltipCorners[0].y < 10f)
        {
            targetPos.y += (10f - tooltipCorners[0].y);
        }

        rectTransform.position = targetPos;
    }

    // Executes fill fallback stats operation.
    // Validates input parameters against null or empty values.
    private static void FillFallbackStats(UIItemDisplayData data)
    {
        if (data == null) return;
        int totalHp = data.baseHp + data.bonusHp;
        int totalAtk = data.baseAtk + data.bonusAtk;
        int totalDef = data.baseDef + data.bonusDef;

        if (totalHp > 0 || totalAtk > 0 || totalDef > 0 || data.bonusCritRate > 0f || data.bonusCritDamage > 0f)
            return;

        int id = data.itemId;
        string name = data.itemName ?? "";

        if (id == 120 || name.IndexOf("Elemental Grimoire", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 40; data.bonusAtk = 16; data.bonusCritRate = 6f; data.bonusCritDamage = 22f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Grimoire containing ancient elemental destruction spells.";
        }
        else if (id == 116 || name.IndexOf("Radiant Guardian Shield", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 180; data.bonusHp = 70; data.baseDef = 45; data.bonusDef = 18;
            if (string.IsNullOrEmpty(data.description)) data.description = "Sacred radiant shield providing massive defense.";
        }
        else if (id == 117 || name.IndexOf("Cloak of Stars", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 120; data.bonusHp = 40; data.baseAtk = 10; data.bonusAtk = 5; data.baseDef = 25; data.bonusDef = 10; data.bonusCritRate = 6f; data.bonusCritDamage = 15f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Mystical cloak woven from starlight and cosmic energy.";
        }
        else if (id == 118 || name.IndexOf("Amulet of Eternal Flame", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 140; data.bonusHp = 50; data.baseAtk = 12; data.bonusAtk = 6; data.baseDef = 15; data.bonusDef = 5; data.bonusCritRate = 8f; data.bonusCritDamage = 18f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Ancient amulet burning with unquenchable flame.";
        }
        else if (id == 119 || name.IndexOf("Paladin Broadsword", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 38; data.bonusAtk = 15; data.bonusCritRate = 8f; data.bonusCritDamage = 20f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Heavy broadsword wielded by holy paladins.";
        }
        else if (id == 121 || name.IndexOf("Shadow Crossbow", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 36; data.bonusAtk = 14; data.bonusCritRate = 9f; data.bonusCritDamage = 18f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Silent crossbow firing dark shadow bolts.";
        }
        else if (id == 122 || name.IndexOf("Fortress Tower Shield", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 110; data.bonusHp = 40; data.baseDef = 28; data.bonusDef = 11;
            if (string.IsNullOrEmpty(data.description)) data.description = "Impenetrable tower shield used by castle vanguards.";
        }
        else if (id == 123 || name.IndexOf("Hood of Silent Night", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 70; data.bonusHp = 25; data.baseAtk = 8; data.bonusAtk = 4; data.baseDef = 16; data.bonusDef = 6; data.bonusCritRate = 7f; data.bonusCritDamage = 15f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Stealth hood worn by night assassins.";
        }
        else if (id == 124 || name.IndexOf("Ring of Tempest", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 60; data.bonusHp = 20; data.baseAtk = 10; data.bonusAtk = 5; data.baseDef = 10; data.bonusDef = 4; data.bonusCritRate = 5f; data.bonusCritDamage = 12f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Ring channeling the power of storm winds.";
        }
        else if (id == 129 || name.IndexOf("Mantle of the Forest", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 50; data.bonusHp = 25; data.baseDef = 12; data.bonusDef = 6;
            if (string.IsNullOrEmpty(data.description)) data.description = "Enchanted mantle imbued with forest spirit blessings.";
        }
        else if (id == 12 || name.IndexOf("Dragon Scale Armor", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 196; data.bonusHp = 84; data.baseDef = 32; data.bonusDef = 14;
            if (string.IsNullOrEmpty(data.description)) data.description = "Heavy armor forged from ancient dragon scales.";
        }
        else if (id == 13 || name.IndexOf("Phantom Cloak", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 84; data.bonusHp = 36; data.baseDef = 14; data.bonusDef = 6;
            if (string.IsNullOrEmpty(data.description)) data.description = "Ethereal cloak enabling swift shadow movements.";
        }
        else if (id == 14 || name.IndexOf("Shadow Hood", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseDef = 7; data.bonusDef = 3; data.bonusCritRate = 8f; data.bonusCritDamage = 25f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Dark assassin hood increasing critical precision.";
        }
        else if (id == 8 || name.IndexOf("Elven Blade", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 29; data.bonusAtk = 13; data.bonusCritRate = 10f; data.bonusCritDamage = 30f;
            if (string.IsNullOrEmpty(data.description)) data.description = "Finely crafted elven blade with sharp edge.";
        }
        else if (id == 31 || name.IndexOf("Magic Flour", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (string.IsNullOrEmpty(data.description)) data.description = "Rare legendary ingredient used for high-tier crafting.";
        }
        else if (id == 5 || name.IndexOf("Iron Sword", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 7; data.bonusAtk = 3; data.bonusCritRate = 3f; data.bonusCritDamage = 15f;
        }
        else if (id == 6 || name.IndexOf("Hunter Bow", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 6; data.bonusAtk = 2; data.bonusCritRate = 6f; data.bonusCritDamage = 10f;
        }
        else if (id == 7 || name.IndexOf("Apprentice Staff", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 6; data.bonusAtk = 3; data.bonusCritRate = 2f; data.bonusCritDamage = 20f;
        }
        else if (id == 9 || name.IndexOf("Leather Armor", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 31; data.bonusHp = 14; data.baseDef = 6; data.bonusDef = 2;
        }
        else if (id == 10 || name.IndexOf("Iron Helmet", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 21; data.bonusHp = 9; data.baseDef = 4; data.bonusDef = 2;
        }
        else if (id == 11 || name.IndexOf("Wind Boots", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseDef = 4; data.bonusDef = 1;
        }
        else if (id == 15 || name.IndexOf("Iron Gauntlets", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 4; data.bonusAtk = 2; data.baseDef = 3; data.bonusDef = 1;
        }
        else if (id == 16 || name.IndexOf("Leather Gauntlets", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseAtk = 3; data.bonusAtk = 1; data.baseDef = 2; data.bonusDef = 1;
        }
        else if (id == 17 || name.IndexOf("Copper Ring", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 18; data.bonusHp = 7; data.baseAtk = 2; data.bonusAtk = 1; data.bonusCritRate = 3f; data.bonusCritDamage = 6f;
        }
        else if (id == 18 || name.IndexOf("Silver Necklace", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            data.baseHp = 35; data.bonusHp = 15; data.baseDef = 4; data.bonusDef = 1;
        }
    }
}
