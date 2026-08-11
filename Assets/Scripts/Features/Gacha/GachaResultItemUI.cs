using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binder for a single pulled-item card in the Gacha ResultPopup
/// (Assets/Prefab/Button/GachaItemTemplate.prefab).
/// Pure wide magical soft aura halo with breathing pulse effect.
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

    private static Sprite _softAuraSprite;

    private Image _auraImage;
    private RectTransform _auraRt;
    private Color _baseRarityColor = Color.white;
    private float _pulseSpeed = 3.2f;

    /// <summary>
    /// Tạo texture hào quang tỏa tròn siêu mịn (Ultra Soft Radial Falloff)
    /// </summary>
    private static Sprite GetSoftAuraSprite()
    {
        if (_softAuraSprite != null) return _softAuraSprite;

        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) / 2f;
        float maxRadius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / maxRadius;
                float dy = (y - center) / maxRadius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Tỏa mượt mềm mại ra môi trường xung quanh
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 1.5f); // Soft wide dissipation

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        _softAuraSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _softAuraSprite;
    }

    /// <summary>
    /// Áp dụng hào quang tỏa rộng (Soft Aura Halo) mềm mại xung quanh thẻ bài
    /// </summary>
    public void ApplyRarityVisuals(string rarity, string colorHex)
    {
        if (!ColorUtility.TryParseHtmlString(colorHex, out Color rarityColor))
        {
            rarityColor = Color.white;
        }
        _baseRarityColor = rarityColor;

        // Dọn dẹp bớt các element thừa cũ nếu có
        Transform oldBadgePill = transform.Find("RarityBadgePill");
        if (oldBadgePill != null) Destroy(oldBadgePill.gameObject);

        Transform oldBadgeText = transform.Find("RarityBadgeText");
        if (oldBadgeText != null) Destroy(oldBadgeText.gameObject);

        Transform oldSparkles = transform.Find("CornerSparkles");
        if (oldSparkles != null) Destroy(oldSparkles.gameObject);

        Transform oldGlow = transform.Find("RarityGlowOuter");
        if (oldGlow != null) Destroy(oldGlow.gameObject);

        // Tốc độ nhấp nháy thở nhẹ
        string rarityLower = (rarity ?? "").Trim().ToLower();
        switch (rarityLower)
        {
            case "mythic":    _pulseSpeed = 4.5f; break;
            case "legendary": _pulseSpeed = 4.0f; break;
            case "epic":      _pulseSpeed = 3.5f; break;
            case "rare":      _pulseSpeed = 3.0f; break;
            default:          _pulseSpeed = 2.2f; break;
        }

        // Tạo hoặc cập nhật Hào quang tỏa rộng (Soft Aura) phía sau thẻ bài
        Transform auraTr = transform.Find("MagicalAuraHalo");
        if (auraTr == null)
        {
            GameObject auraGo = new GameObject("MagicalAuraHalo", typeof(RectTransform), typeof(Image));
            auraGo.transform.SetParent(transform, false);
            auraGo.transform.SetAsFirstSibling(); // Thấp hơn card để làm nền hào quang phía sau

            _auraRt = auraGo.GetComponent<RectTransform>();
            _auraRt.anchorMin = Vector2.zero;
            _auraRt.anchorMax = Vector2.one;
            _auraRt.offsetMin = new Vector2(-45, -45); // Tỏa rộng 45px xung quanh thẻ bài
            _auraRt.offsetMax = new Vector2(45, 45);

            _auraImage = auraGo.GetComponent<Image>();
            _auraImage.sprite = GetSoftAuraSprite();
            _auraImage.type = Image.Type.Simple;
            _auraImage.raycastTarget = false;
        }
        else
        {
            _auraRt = auraTr.GetComponent<RectTransform>();
            _auraImage = auraTr.GetComponent<Image>();
        }

        if (_auraImage != null)
        {
            _auraImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.85f);
        }
    }

    private void Update()
    {
        // Hiệu ứng thở hào quang tỏa mượt (Soft Aura Breathing Pulse)
        if (_auraImage != null && _auraRt != null && gameObject.activeInHierarchy)
        {
            float sin = Mathf.Sin(Time.time * _pulseSpeed);
            float scale = 1.0f + (sin * 0.08f);   // Co giãn hào quang 1.0 → 1.08
            float alpha = 0.75f + (sin * 0.20f);  // Nhấp nháy nhẹ độ mờ 0.55 → 0.95

            _auraRt.localScale = new Vector3(scale, scale, 1f);
            _auraImage.color = new Color(_baseRarityColor.r, _baseRarityColor.g, _baseRarityColor.b, alpha);
        }
    }

    /// <summary>
    /// Hiện badge số lượng, tô theo màu độ hiếm của vật phẩm.
    /// count <= 1 thì ẩn hẳn GameObject để card đơn lẻ trông như cũ.
    /// </summary>
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
