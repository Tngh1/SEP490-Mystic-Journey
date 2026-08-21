using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Executes mono behaviour operation.
public class UIChatMessage : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text senderText;
    public TMP_Text messageText;
    public Button senderButton;
    public Button reportButton;

    public Image background;

    public Action<string, int, Vector3> OnSenderClicked;
    public Action<UIChatMessage> OnReportClicked;

    private string currentSender;
    private int currentSenderProfileId;
    private bool senderListenerBound;
    private bool reportListenerBound;

    // Executes chat message id operation.
    public int ChatMessageId { get; private set; }
    // Executes can report operation.
    public bool CanReport { get; private set; }
    // Executes sender profile id operation.
    public int SenderProfileId => currentSenderProfileId;
    public string MessageContent { get; private set; }
    public string Channel { get; private set; } = "World";

    // Initializes internal component caches and dependencies for UIChatMessage upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        BindSenderButton();
        BindReportButton();
    }

    // Executes setup operation.
    public void Setup(string sender, string message, Color senderColor, Color bgColor)
    {
        Setup(sender, message, senderColor, bgColor, 0, 0, true, false);
    }

    // Update up using sender, message, sender color, and bg color and returns the computed result.
    public void Setup(
        string sender,
        string message,
        Color senderColor,
        Color bgColor,
        int chatMessageId,
        bool isMine,
        bool isReported)
    {
        Setup(sender, message, senderColor, bgColor, chatMessageId, 0, isMine, isReported);
    }

    // Update up using sender, message, sender color, and bg color; it updates report button state and guards invalid or unavailable states.
    public void Setup(
        string sender,
        string message,
        Color senderColor,
        Color bgColor,
        int chatMessageId,
        int senderProfileId,
        bool isMine,
        bool isReported,
        string channel = "World")
    {
        currentSender = sender;
        currentSenderProfileId = senderProfileId;
        ChatMessageId = chatMessageId;
        MessageContent = message ?? string.Empty;
        Channel = string.IsNullOrWhiteSpace(channel) ? "World" : channel;

        CanReport = !isMine && !isReported;

        if (senderText != null)
        {
            senderText.text = sender + ":";
            senderText.color = senderColor;
            senderText.raycastTarget = false;
        }

        if (messageText != null)
        {
            messageText.text = message;
            messageText.raycastTarget = false;
        }

        if (background != null && bgColor.a > 0)
        {
            background.color = bgColor;
        }

        EnsureSenderButton();
        EnsureReportButton();
        UpdateReportButtonState(isReported);
    }

    // Executes mark reported operation.
    public void MarkReported()
    {
        CanReport = false;
        UpdateReportButtonState(true);
    }

    // Executes bind sender button operation.
    private void BindSenderButton()
    {
        if (senderListenerBound || senderButton == null)
        {
            return;
        }

        senderListenerBound = true;
        senderButton.onClick.AddListener(HandleSenderClicked);
    }

    // Executes bind report button operation.
    private void BindReportButton()
    {
        if (reportListenerBound || reportButton == null)
        {
            return;
        }

        reportListenerBound = true;
        reportButton.onClick.AddListener(HandleReportClicked);
    }

    // Executes handle sender clicked operation.
    private void HandleSenderClicked()
    {
        if (senderButton != null)
        {
            OnSenderClicked?.Invoke(currentSender, currentSenderProfileId, senderButton.transform.position);
        }
    }

    // Executes handle report clicked operation.
    private void HandleReportClicked()
    {
        if (!CanReport)
        {
            return;
        }

        OnReportClicked?.Invoke(this);
    }

    // Executes ensure sender button operation.
    private void EnsureSenderButton()
    {
        if (senderText != null)
        {
            senderText.raycastTarget = false;
            foreach (var childText in senderText.GetComponentsInChildren<TMP_Text>(true))
            {
                childText.raycastTarget = false;
            }
            foreach (var childLegacyText in senderText.GetComponentsInChildren<Text>(true))
            {
                childLegacyText.raycastTarget = false;
            }
        }

        if (senderButton != null)
        {
            foreach (var img in senderButton.GetComponentsInChildren<Image>(true))
            {
                img.raycastTarget = true;
                if (senderButton.targetGraphic == null)
                {
                    senderButton.targetGraphic = img;
                }
            }

            var graphic = senderButton.targetGraphic as Graphic;
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }

            if (senderButton.GetComponent<UIHoverScaleEffect>() == null)
            {
                senderButton.gameObject.AddComponent<UIHoverScaleEffect>();
            }
            BindSenderButton();
            return;
        }

        if (senderText == null) return;

        var buttonObject = new GameObject("AutoSenderButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(senderText.transform, false);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-6f, -6f);
        rect.offsetMax = new Vector2(6f, 6f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0, 0, 0, 0);
        image.raycastTarget = true;

        senderButton = buttonObject.GetComponent<Button>();
        senderButton.targetGraphic = image;
        if (senderButton.GetComponent<UIHoverScaleEffect>() == null)
        {
            senderButton.gameObject.AddComponent<UIHoverScaleEffect>();
        }
        BindSenderButton();
    }

    // Executes ensure report button operation.
    private void EnsureReportButton()
    {
        if (reportButton != null)
        {
            var le = reportButton.GetComponent<LayoutElement>();
            if (le == null) le = reportButton.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            var rt = reportButton.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-10f, 0f);
                rt.sizeDelta = new Vector2(22f, 22f);
                rt.localScale = Vector3.one;
            }

            if (CanReport)
            {
                foreach (var img in reportButton.GetComponentsInChildren<Image>(true))
                {
                    img.enabled      = true;
                    img.raycastTarget = true;
                    if (img.gameObject == reportButton.gameObject)
                    {
                        img.color = Color.white;
                        if (reportButton.targetGraphic == null)
                        {
                            reportButton.targetGraphic = img;
                        }
                    }
                }

                foreach (var textComponent in reportButton.GetComponentsInChildren<TMP_Text>(true))
                {
                    textComponent.raycastTarget = false;
                }

                if (reportButton.GetComponent<UIHoverScaleEffect>() == null)
                {
                    reportButton.gameObject.AddComponent<UIHoverScaleEffect>();
                }

                reportButton.transform.SetAsLastSibling();

                Transform t = reportButton.transform.parent;
                while (t != null && t != transform)
                {
                    if (!t.gameObject.activeSelf)
                        t.gameObject.SetActive(true);
                    t = t.parent;
                }
            }

            BindReportButton();
            return;
        }

        if (!CanReport)
        {
            return;
        }

        var buttonObject = new GameObject("ReportButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var dynamicLe = buttonObject.GetComponent<LayoutElement>();
        dynamicLe.ignoreLayout = true;

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-6f, 3f);
        rect.sizeDelta = new Vector2(52f, 24f);
        rect.localScale = Vector3.one;

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.55f, 0.16f, 0.16f, 0.88f);
        image.raycastTarget = true;

        reportButton = buttonObject.GetComponent<Button>();
        if (reportButton.GetComponent<UIHoverScaleEffect>() == null)
        {
            reportButton.gameObject.AddComponent<UIHoverScaleEffect>();
        }

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "Report";
        label.fontSize = 11f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        buttonObject.transform.SetAsLastSibling();
        BindReportButton();
    }

    // Executes update report button state operation.
    private void UpdateReportButtonState(bool isReported)
    {
        if (reportButton == null)
        {
            return;
        }

        reportButton.gameObject.SetActive(CanReport || isReported);
        reportButton.interactable = CanReport;

        var label = reportButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = isReported ? "Reported" : "Report";
        }
    }
}
