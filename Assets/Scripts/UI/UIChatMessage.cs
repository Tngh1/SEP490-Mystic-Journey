using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    public int ChatMessageId { get; private set; }
    public bool CanReport { get; private set; }
    public int SenderProfileId => currentSenderProfileId;

    private void Awake()
    {
        BindSenderButton();
        BindReportButton();
    }

    public void Setup(string sender, string message, Color senderColor, Color bgColor)
    {
        Setup(sender, message, senderColor, bgColor, 0, 0, true, false);
    }

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

    public void Setup(
        string sender,
        string message,
        Color senderColor,
        Color bgColor,
        int chatMessageId,
        int senderProfileId,
        bool isMine,
        bool isReported)
    {
        currentSender = sender;
        currentSenderProfileId = senderProfileId;
        ChatMessageId = chatMessageId;
        
        // Chỉ hiện nút Report cho tin nhắn của ng khác.
        CanReport = chatMessageId > 0 && !isMine && !isReported;

        if (senderText != null)
        {
            senderText.text = sender + ":";
            senderText.color = senderColor;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (background != null && bgColor.a > 0)
        {
            background.color = bgColor;
        }

        EnsureSenderButton();
        EnsureReportButton();
        UpdateReportButtonState(isReported);
    }

    public void MarkReported()
    {
        CanReport = false;
        UpdateReportButtonState(true);
    }

    private void BindSenderButton()
    {
        if (senderListenerBound || senderButton == null)
        {
            return;
        }

        senderListenerBound = true;
        senderButton.onClick.AddListener(HandleSenderClicked);
    }

    private void BindReportButton()
    {
        if (reportListenerBound || reportButton == null)
        {
            return;
        }

        reportListenerBound = true;
        reportButton.onClick.AddListener(HandleReportClicked);
    }

    private void HandleSenderClicked()
    {
        if (senderButton != null)
        {
            OnSenderClicked?.Invoke(currentSender, currentSenderProfileId, senderButton.transform.position);
        }
    }

    private void HandleReportClicked()
    {
        if (!CanReport)
        {
            return;
        }

        OnReportClicked?.Invoke(this);
    }

    private void EnsureSenderButton()
    {
        if (senderButton != null)
        {
            BindSenderButton();
            return;
        }

        if (senderText == null) return;

        var buttonObject = new GameObject("AutoSenderButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(senderText.transform, false);
        
        // Căng tràn lên senderText
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0, 0, 0, 0); // Vô hình hoàn toàn
        image.raycastTarget = true;

        senderButton = buttonObject.GetComponent<Button>();
        BindSenderButton();
    }

    private void EnsureReportButton()
    {
        if (reportButton != null)
        {
            if (CanReport)
            {
                // Thoát khỏi Layout Group nếu có (LayoutGroup sẽ đè vị trí ta set thủ công)
                var le = reportButton.GetComponent<LayoutElement>();
                if (le == null) le = reportButton.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                var rt = reportButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(1f, 0.5f);
                    rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot     = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(-4f, 0f);
                    rt.sizeDelta = new Vector2(58f, 24f);
                }

                // Fix màu: button gốc + mọi Image con
                foreach (var img in reportButton.GetComponentsInChildren<Image>(true))
                {
                    if (img.gameObject == reportButton.gameObject)
                    {
                        img.color        = new Color(0.55f, 0.16f, 0.16f, 0.88f);
                        img.enabled      = true;
                        img.raycastTarget = true;
                    }
                }

                // Render trên cùng để không bị che bởi background
                reportButton.transform.SetAsLastSibling();

                // Kích hoạt chain cha nếu bị ẩn trong prefab
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
        var buttonObject = new GameObject("ReportButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-6f, -4f);
        rect.sizeDelta = new Vector2(58f, 24f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.55f, 0.16f, 0.16f, 0.88f);
        image.raycastTarget = true;

        reportButton = buttonObject.GetComponent<Button>();

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "Report";
        label.fontSize = 12f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        BindReportButton();
    }

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
