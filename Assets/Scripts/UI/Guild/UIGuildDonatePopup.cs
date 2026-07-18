using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;

namespace MysticJourney.UI.Guild
{
    public class UIGuildDonatePopup : MonoBehaviour
    {
        private TMP_InputField amountInput;
        private Button btnConfirm;
        private Button btnCancel;
        private Button btnMax;
        private Button btnMin;
        private TextMeshProUGUI txtTitle;
        private TextMeshProUGUI txtMessage;

        private int _guildId;
        private Action _onSuccess;

        public static UIGuildDonatePopup CreateRuntime(Transform parent)
        {
            // Panel Root
            var go = new GameObject("GuildDonatePopup");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            
            // BG Image (Dark overlay)
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.8f);

            // Inner Window
            var windowGo = new GameObject("Window");
            var windowRect = windowGo.AddComponent<RectTransform>();
            windowRect.SetParent(rect, false);
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(450, 300);
            
            var windowImg = windowGo.AddComponent<Image>();
            windowImg.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            var popup = go.AddComponent<UIGuildDonatePopup>();

            // Title
            var titleText = CreateText(windowRect, "Title", "Donate Gold", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(300, 50), new Vector2(0, -30), 24, true);
            
            // Amount Label
            CreateText(windowRect, "AmountLabel", "Enter amount to donate (Cost: Gold):", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 30), new Vector2(0, 40), 16, false);
            
            // Input Field
            var inputGo = new GameObject("InputField");
            var inputRect = inputGo.AddComponent<RectTransform>();
            inputRect.SetParent(windowRect, false);
            inputRect.sizeDelta = new Vector2(200, 40);
            inputRect.anchoredPosition = new Vector2(0, 0);
            var inputImg = inputGo.AddComponent<Image>();
            inputImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var inputField = inputGo.AddComponent<TMP_InputField>();
            
            var textGo = new GameObject("Text");
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.SetParent(inputRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-10, -10);
            var txtVal = textGo.AddComponent<TextMeshProUGUI>();
            txtVal.color = Color.white;
            txtVal.fontSize = 18;
            txtVal.alignment = TextAlignmentOptions.Center;
            inputField.textComponent = txtVal;
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.text = "10";
            popup.amountInput = inputField;

            // Message Label (for errors/success)
            popup.txtMessage = CreateText(windowRect, "Message", "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 40), new Vector2(0, -40), 14, false);
            popup.txtMessage.color = Color.yellow;

            // Buttons
            popup.btnCancel = CreateButton(windowRect, "CancelButton", "Cancel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120, 45), new Vector2(-70, 40), new Color(0.4f, 0.1f, 0.1f, 1f));
            popup.btnConfirm = CreateButton(windowRect, "ConfirmButton", "Donate", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120, 45), new Vector2(70, 40), new Color(0.1f, 0.4f, 0.1f, 1f));

            // Max/Min shortcut buttons
            popup.btnMin = CreateButton(windowRect, "MinButton", "-10", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 30), new Vector2(-130, 0), new Color(0.3f, 0.3f, 0.3f, 1f));
            popup.btnMax = CreateButton(windowRect, "MaxButton", "+10", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 30), new Vector2(130, 0), new Color(0.3f, 0.3f, 0.3f, 1f));

            popup.InitializeBindings();
            go.SetActive(false);
            return popup;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos, float fontSize, bool bold)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos, Color color)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            CreateText(rect, "Text", text, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16, true);
            return btn;
        }

        private void InitializeBindings()
        {
            btnCancel.onClick.AddListener(ClosePopup);
            btnConfirm.onClick.AddListener(OnConfirmClicked);
            btnMin.onClick.AddListener(() => ChangeAmount(-10));
            btnMax.onClick.AddListener(() => ChangeAmount(10));
        }

        private void ChangeAmount(int delta)
        {
            if (int.TryParse(amountInput.text, out int current))
            {
                int newAmount = Mathf.Max(1, current + delta);
                amountInput.text = newAmount.ToString();
            }
            else
            {
                amountInput.text = "10";
            }
        }

        public void Open(int guildId, Action onSuccess)
        {
            _guildId = guildId;
            _onSuccess = onSuccess;
            amountInput.text = "10";
            txtMessage.text = "";
            btnConfirm.interactable = true;
            gameObject.SetActive(true);
            
            // Pop to top
            transform.SetAsLastSibling();
        }

        public void ClosePopup()
        {
            gameObject.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            if (!int.TryParse(amountInput.text, out int amount) || amount <= 0)
            {
                txtMessage.text = "Please enter a valid amount.";
                return;
            }

            btnConfirm.interactable = false;
            txtMessage.text = "Processing donation...";

            GuildApi.Donate(_guildId, amount,
                result =>
                {
                    txtMessage.text = $"Success! Gained {result.guildExpGained} EXP.";
                    btnConfirm.interactable = true;
                    _onSuccess?.Invoke();
                    
                    // Automatically close after success with small delay
                    Invoke(nameof(ClosePopup), 1.5f);
                },
                error =>
                {
                    txtMessage.text = $"Error: {error.Message}";
                    btnConfirm.interactable = true;
                });
        }
    }
}
