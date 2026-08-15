using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIShopItemTooltip : MonoBehaviour
{
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

        EnsureStatIconsAsset();
        if (container != null) container.SetActive(false);
        else gameObject.SetActive(false);
    }

    public static UIShopItemTooltip GetOrCreate(Canvas targetCanvas = null)
    {
        if (Instance != null) return Instance;

        // Try finding in scene
        Instance = FindFirstObjectByType<UIShopItemTooltip>(FindObjectsInactive.Include);
        if (Instance != null)
        {
            Instance.gameObject.SetActive(true);
            return Instance;
        }

        // Build runtime UI floating tooltip
        Canvas canvas = targetCanvas;
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

        if (canvas == null) return null;

        GameObject tooltipObj = new GameObject("UIShopItemTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(UIShopItemTooltip));
        tooltipObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = tooltipObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 220f);
        rect.pivot = new Vector2(0f, 1f);

        // Background
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bgObj.GetComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        // Border / Frame
        GameObject borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;

        Image borderImage = borderObj.GetComponent<Image>();
        borderImage.color = new Color(1f, 0.8f, 0.2f, 0.6f);

        // Content Layout
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

        // Name
        GameObject nameObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(contentObj.transform, false);
        TMP_Text nameTextComp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTextComp.fontSize = 16f;
        nameTextComp.fontStyle = FontStyles.Bold;

        // Type & Rarity
        GameObject typeObj = new GameObject("TypeRarityText", typeof(RectTransform), typeof(TextMeshProUGUI));
        typeObj.transform.SetParent(contentObj.transform, false);
        TMP_Text typeTextComp = typeObj.GetComponent<TextMeshProUGUI>();
        typeTextComp.fontSize = 12f;
        typeTextComp.color = new Color(0.7f, 0.7f, 0.8f);

        // Stats
        GameObject statsObj = new GameObject("StatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statsObj.transform.SetParent(contentObj.transform, false);
        TMP_Text statsTextComp = statsObj.GetComponent<TextMeshProUGUI>();
        statsTextComp.fontSize = 13f;
        statsTextComp.richText = true;

        // Description
        GameObject descObj = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descObj.transform.SetParent(contentObj.transform, false);
        TMP_Text descTextComp = descObj.GetComponent<TextMeshProUGUI>();
        descTextComp.fontSize = 11f;
        descTextComp.fontStyle = FontStyles.Italic;
        descTextComp.color = new Color(0.85f, 0.85f, 0.9f);

        // Price & Limit
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

    private void EnsureStatIconsAsset()
    {
        if (statSpriteAsset == null)
            statSpriteAsset = Resources.Load<TMP_SpriteAsset>("Sprite Assets/StatIcons");

        if (statSpriteAsset != null && statsText != null)
        {
            statsText.spriteAsset = statSpriteAsset;
        }
    }

    public void ShowTooltip(UIItemDisplayData data, RectTransform slotTransform)
    {
        if (data == null)
        {
            HideTooltip();
            return;
        }

        EnsureStatIconsAsset();

        // 1. Title & Rarity Color
        Color rarityColor = UIItemDetailPopup.GetRarityColor(data.rarity);
        if (nameText != null)
        {
            nameText.text = data.itemName ?? "Unknown Item";
            nameText.color = rarityColor;
        }

        if (rarityBorder != null)
        {
            rarityBorder.color = rarityColor;
        }

        // 2. Type & Rarity Header
        if (typeRarityText != null)
        {
            string categoryOrSlot = !string.IsNullOrEmpty(data.slot) && !data.slot.Equals("None", StringComparison.OrdinalIgnoreCase)
                ? data.slot
                : (!string.IsNullOrEmpty(data.category) ? data.category : "Item");
            string rarityStr = !string.IsNullOrEmpty(data.rarity) ? data.rarity : "Common";
            typeRarityText.text = $"{rarityStr} • {categoryOrSlot}";
        }

        // 3. Equipment Stats with Stat Icons (<sprite name="...">)
        if (statsText != null)
        {
            StringBuilder sb = new StringBuilder();

            int totalHp = data.baseHp + data.bonusHp;
            int totalAtk = data.baseAtk + data.bonusAtk;
            int totalDef = data.baseDef + data.bonusDef;

            if (totalHp > 0)
                sb.AppendLine($"<sprite name=\"HPStats\"> <color=#FF5555>HP:</color> +{totalHp}");

            if (totalAtk > 0)
                sb.AppendLine($"<sprite name=\"DMGStats\"> <color=#FFB86C>ATK:</color> +{totalAtk}");

            if (totalDef > 0)
                sb.AppendLine($"<sprite name=\"DEFStats\"> <color=#8BE9FD>DEF:</color> +{totalDef}");

            if (data.bonusCritRate > 0f)
                sb.AppendLine($"<sprite name=\"CritStats\"> <color=#50FA7B>Crit Rate:</color> +{data.bonusCritRate:F1}%");

            if (data.bonusCritDamage > 0f)
                sb.AppendLine($"<sprite name=\"CritDMGStats\"> <color=#BD93F9>Crit Dmg:</color> +{data.bonusCritDamage:F1}%");

            if (data.corruptionReduction > 0f)
                sb.AppendLine($"<color=yellow>Corruption Reduction:</color> -{data.corruptionReduction:F1}");

            statsText.text = sb.ToString().TrimEnd();
            statsText.gameObject.SetActive(statsText.text.Length > 0);
        }

        // 4. Description
        if (descriptionText != null)
        {
            descriptionText.text = data.description ?? string.Empty;
            descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.description));
        }

        // 5. Price & Limit Info
        if (priceLimitText != null)
        {
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

        // 6. Display & Position Tooltip near Slot
        if (container != null) container.SetActive(true);
        else gameObject.SetActive(true);

        transform.SetAsLastSibling();
        PositionNearSlot(slotTransform);
    }

    public void HideTooltip()
    {
        if (container != null) container.SetActive(false);
        else gameObject.SetActive(false);
    }

    private void PositionNearSlot(RectTransform slotTransform)
    {
        if (slotTransform == null || rectTransform == null) return;

        Vector3[] corners = new Vector3[4];
        slotTransform.GetWorldCorners(corners);

        // Top-right corner of slot
        Vector3 targetPos = corners[2];
        rectTransform.position = targetPos;

        // Keep inside screen boundaries
        Vector3[] tooltipCorners = new Vector3[4];
        rectTransform.GetWorldCorners(tooltipCorners);

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // If off right screen, flip to left of slot
        if (tooltipCorners[2].x > screenWidth)
        {
            targetPos.x = corners[0].x - rectTransform.rect.width;
        }

        // If off bottom screen, adjust upwards
        if (tooltipCorners[0].y < 0)
        {
            targetPos.y += (0 - tooltipCorners[0].y) + 10f;
        }

        rectTransform.position = targetPos;
    }
}
