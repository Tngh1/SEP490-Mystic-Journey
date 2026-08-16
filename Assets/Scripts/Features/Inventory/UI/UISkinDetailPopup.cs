using System;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISkinDetailPopup : MonoBehaviour
{
    [Header("Skin Panel - Container")]
    [SerializeField] private GameObject skinPanel;

    [Header("Skin Panel - Fields")]
    [SerializeField] private TMP_Text skinTitleText;
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private Image skinIcon;
    
    [Header("Skin Panel - Buttons")]
    [SerializeField] private Button confirmSkinButton;
    [SerializeField] private Button cancelSkinButton;

    private PlayerSkinSummaryResponse _currentSkin;
    private Sprite _currentIcon;

    public Action<PlayerSkinSummaryResponse> OnEquipSkinClicked;
    public Action<PlayerSkinSummaryResponse> OnUnequipSkinClicked;

    private void Awake()
    {
        if (skinPanel != null && !skinPanel.transform.IsChildOf(this.transform))
        {
            skinPanel = null;
        }

        if (skinPanel == null)
        {
            var t = transform.Find("Container/SkinPanel");
            if (t != null) skinPanel = t.gameObject;
        }

        if (confirmSkinButton == null)
        {
            var t = transform.Find("Container/SkinPanel/UseButton");
            if (t) confirmSkinButton = t.GetComponent<Button>();
        }

        if (cancelSkinButton == null)
        {
            var t = transform.Find("Container/SkinPanel/CancelButton");
            if (t) cancelSkinButton = t.GetComponent<Button>();
        }

        if (skinTitleText == null)
        {
            var t = transform.Find("Container/SkinPanel/SkinTitle");
            if (t) skinTitleText = t.GetComponent<TMP_Text>();
        }

        if (skinNameText == null)
        {
            var t = transform.Find("Container/SkinPanel/SkinName");
            if (t) skinNameText = t.GetComponent<TMP_Text>();
        }

        // Force find the correct Icon object, ignoring incorrect Inspector assignments
        var iconTransform = transform.Find("Container/SkinPanel/SkinIcon_Frame/Icon");
        if (iconTransform != null) 
        {
            skinIcon = iconTransform.GetComponent<Image>();
        }
        else
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                // Must be the actual Icon child, not the Frame
                if (img.gameObject.name == "Icon")
                {
                    skinIcon = img;
                    break;
                }
            }
        }
        
        if (skinIcon != null)
            Debug.Log($"[UISkinDetailPopup] Successfully found skinIcon: {skinIcon.gameObject.name}");
        else
            Debug.LogError("[UISkinDetailPopup] CRITICAL: Could not find skinIcon Image component in prefab!");

        if (confirmSkinButton) confirmSkinButton.onClick.AddListener(HandleSkinConfirmed);
        if (cancelSkinButton)  cancelSkinButton.onClick.AddListener(Hide);
    }

    public void ShowSkinDetails(PlayerSkinSummaryResponse skin, Sprite icon)
    {
        gameObject.SetActive(true); // Triggers Awake() on first run if inactive

        _currentSkin = skin;
        _currentIcon = icon;

        if (skinPanel) skinPanel.SetActive(true);

        if (skinIcon)
        {
            skinIcon.sprite = icon;
            skinIcon.enabled = icon != null;
            skinIcon.preserveAspect = true;
            skinIcon.color = Color.white;

            var effect = skinIcon.GetComponent<UIRarityFrameEffect>() ?? skinIcon.GetComponentInParent<UIRarityFrameEffect>();
            if (effect != null) effect.SetVisible(false);

            Debug.Log($"[UISkinDetailPopup] Setting skinIcon ({skinIcon.gameObject.name}) to sprite: {(icon ? icon.name : "NULL")}");
        }
        else
        {
            Debug.LogWarning("[UISkinDetailPopup] skinIcon is NULL!");
        }

        if (skinNameText)
        {
            skinNameText.enableWordWrapping = true;
            skinNameText.enableAutoSizing = true;
            skinNameText.fontSizeMin = 10f;
            skinNameText.fontSizeMax = 20f;
            skinNameText.overflowMode = TextOverflowModes.Ellipsis;
            skinNameText.text = skin.SkinName;
        }

        if (skinTitleText)
        {
            skinTitleText.text = skin.IsEquipped ? "Remove Cosmetic" : "Use Cosmetic";
        }
        
        // Đảm bảo cancel button luôn hiển thị khi popup được mở
        if (cancelSkinButton) cancelSkinButton.gameObject.SetActive(true);

        if (confirmSkinButton)
        {
            bool isDefaultSkin = skin.SkinName != null && skin.SkinName.IndexOf("Default", StringComparison.OrdinalIgnoreCase) >= 0;

            // Ẩn nút nếu: chưa sở hữu HOẶC (đang dùng VÀ là skin mặc định)
            if (skin.PlayerSkinId <= 0 || (skin.IsEquipped && isDefaultSkin))
            {
                confirmSkinButton.gameObject.SetActive(false);
            }
            else
            {
                confirmSkinButton.gameObject.SetActive(true);
                
                var tmp = confirmSkinButton.GetComponentInChildren<TMP_Text>();
                if (tmp)
                {
                    tmp.text = skin.IsEquipped ? "Remove" : "Use";
                }
            }
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _currentSkin = null;
    }

    private void HandleSkinConfirmed()
    {
        if (_currentSkin == null) return;
        
        if (_currentSkin.IsEquipped)
        {
            OnUnequipSkinClicked?.Invoke(_currentSkin);
        }
        else
        {
            OnEquipSkinClicked?.Invoke(_currentSkin);
        }
    }
}
