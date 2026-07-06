using System;
using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Friend
{
    public class UIFriendChatPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button closeButton;

        [Header("Messages")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform messageContainer;
        [SerializeField] private UIChatMessage messagePrefab;

        [Header("Input")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;

        [Header("History")]
        [SerializeField] private int historyPageSize = 50;
        [SerializeField] private float refreshInterval = 3f;

        [Header("Colors")]
        [SerializeField] private Color myNameColor = Color.yellow;
        [SerializeField] private Color friendNameColor = Color.cyan;
        [SerializeField] private Color systemNameColor = Color.gray;

        private readonly HashSet<int> displayedMessageIds = new HashSet<int>();
        private readonly HashSet<int> pendingReportIds = new HashSet<int>();
        private int friendProfileId;
        private string friendDisplayName;
        private bool isLoadingHistory;
        private bool isSending;
        private bool eventsBound;
        private Coroutine refreshCoroutine;

        public static UIFriendChatPanel CreateRuntime(Transform owner, UIChatMessage fallbackMessagePrefab)
        {
            Transform parent = owner != null && owner.GetComponentInParent<Canvas>() != null
                ? owner.GetComponentInParent<Canvas>().transform
                : owner;

            var root = CreateRect("FriendChatPanel_Runtime", parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(560f, 520f);
            root.anchoredPosition = Vector2.zero;

            var panelImage = root.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.07f, 0.09f, 0.96f);

            var panel = root.gameObject.AddComponent<UIFriendChatPanel>();
            panel.messagePrefab = fallbackMessagePrefab;

            panel.titleText = CreateText("Title", root, "Friend Chat", 20, FontStyles.Bold, TextAlignmentOptions.Left);
            SetRect(panel.titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(16f, -18f), new Vector2(-96f, 32f));

            panel.closeButton = CreateButton("CloseButton", root, "X");
            SetRect(panel.closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(44f, 32f));

            panel.statusText = CreateText("Status", root, string.Empty, 13, FontStyles.Normal, TextAlignmentOptions.Left);
            panel.statusText.color = new Color(1f, 0.75f, 0.35f, 1f);
            SetRect(panel.statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(16f, -52f), new Vector2(-32f, 24f));

            var scrollRoot = CreateRect("Messages", root);
            SetRect(scrollRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(16f, 72f), new Vector2(-32f, -136f));
            scrollRoot.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.22f);
            panel.scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            panel.scrollRect.horizontal = false;

            var viewport = CreateRect("Viewport", scrollRoot);
            SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.04f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var verticalLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(8, 8, 8, 8);
            verticalLayout.spacing = 6f;
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlHeight = true;
            verticalLayout.childControlWidth = true;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childForceExpandWidth = true;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            panel.messageContainer = content;
            panel.scrollRect.viewport = viewport;
            panel.scrollRect.content = content;

            panel.inputField = CreateInput("Input", root);
            SetRect(panel.inputField.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(16f, 20f), new Vector2(-112f, 40f));

            panel.sendButton = CreateButton("SendButton", root, "Send");
            SetRect(panel.sendButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 20f), new Vector2(84f, 40f));

            panel.BindEvents();
            root.gameObject.SetActive(false);
            return panel;
        }

        private void Awake()
        {
            BindEvents();
        }

        private void BindEvents()
        {
            if (eventsBound)
            {
                return;
            }

            if (sendButton == null && closeButton == null && inputField == null)
            {
                return;
            }

            eventsBound = true;

            if (sendButton != null)
            {
                sendButton.onClick.AddListener(OnSendClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (inputField != null)
            {
                inputField.onSubmit.AddListener(_ => OnSendClicked());
            }
        }

        private void OnDisable()
        {
            StopRefresh();
        }

        public void Open(int targetFriendProfileId, string targetFriendName)
        {
            friendProfileId = targetFriendProfileId;
            friendDisplayName = string.IsNullOrWhiteSpace(targetFriendName)
                ? $"Player {targetFriendProfileId}"
                : targetFriendName;

            gameObject.SetActive(true);
            displayedMessageIds.Clear();
            pendingReportIds.Clear();
            ClearMessages();
            SetSending(false);

            if (titleText != null)
            {
                titleText.text = friendDisplayName;
            }

            SetStatus(string.Empty);
            LoadHistory();
            StartRefresh();
            FocusInput();
        }

        public void Close()
        {
            StopRefresh();
            gameObject.SetActive(false);
        }

        private void OnSendClicked()
        {
            if (isSending || friendProfileId <= 0 || inputField == null)
            {
                return;
            }

            string content = inputField.text != null ? inputField.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                FocusInput();
                return;
            }

            if (!ApiClient.Instance.HasToken())
            {
                AddSystemMessage("Please login before using friend chat.");
                FocusInput();
                return;
            }

            inputField.text = string.Empty;
            SetSending(true);

            ChatApi.Instance.SendFriendMessage(
                friendProfileId,
                content,
                message =>
                {
                    SetSending(false);
                    AddFriendMessage(message);
                    FocusInput();
                },
                error =>
                {
                    SetSending(false);
                    inputField.text = content;
                    AddSystemMessage(BuildErrorMessage(error));
                    FocusInput();
                });
        }

        private void LoadHistory()
        {
            if (isLoadingHistory || friendProfileId <= 0 || !isActiveAndEnabled)
            {
                return;
            }

            if (!ApiClient.Instance.HasToken())
            {
                return;
            }

            isLoadingHistory = true;
            int safePageSize = Mathf.Clamp(historyPageSize, 1, 100);

            ChatApi.Instance.GetFriendMessages(
                friendProfileId,
                1,
                safePageSize,
                response =>
                {
                    isLoadingHistory = false;
                    PopulateHistory(response);
                },
                error =>
                {
                    isLoadingHistory = false;
                    SetStatus(BuildErrorMessage(error));
                    Debug.LogWarning($"[UIFriendChatPanel] Load friend chat failed: {error}");
                });
        }

        private void PopulateHistory(PagedResultResponse<FriendChatMessageResponse> response)
        {
            if (response?.Items == null)
            {
                return;
            }

            foreach (var message in response.Items)
            {
                AddFriendMessage(message);
            }
        }

        private void AddFriendMessage(FriendChatMessageResponse message)
        {
            if (message == null || message.IsHidden || string.IsNullOrWhiteSpace(message.Content))
            {
                return;
            }

            if (message.ChatMessageId > 0 && !displayedMessageIds.Add(message.ChatMessageId))
            {
                return;
            }

            bool isMe = IsCurrentPlayer(message.SenderId);
            string sender = ResolveSenderName(message, isMe);
            AddMessage(sender, message.Content, isMe ? myNameColor : friendNameColor, message.ChatMessageId, isMe, message.IsReported);
        }

        private void AddSystemMessage(string message)
        {
            AddMessage("System", message, systemNameColor, 0, true, false);
        }

        private void AddMessage(
            string sender,
            string message,
            Color senderColor,
            int chatMessageId,
            bool isMine,
            bool isReported)
        {
            if (messageContainer == null)
            {
                Debug.LogWarning("[UIFriendChatPanel] Missing message container.");
                return;
            }

            if (messagePrefab != null)
            {
                UIChatMessage item = Instantiate(messagePrefab, messageContainer);
                item.Setup(sender, message, senderColor, new Color(0, 0, 0, 0), chatMessageId, isMine, isReported);
                item.OnReportClicked += HandleFriendReportClicked;
            }
            else
            {
                CreateRuntimeMessage(sender, message, senderColor, chatMessageId, isMine, isReported);
            }

            StartCoroutine(ScrollToBottom());
        }

        private void CreateRuntimeMessage(
            string sender,
            string message,
            Color senderColor,
            int chatMessageId,
            bool isMine,
            bool isReported)
        {
            var row = CreateRect("FriendChatMessage", messageContainer);
            row.sizeDelta = new Vector2(0f, 42f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            var senderText = CreateText("Sender", row, sender + ":", 14, FontStyles.Bold, TextAlignmentOptions.Left);
            senderText.color = senderColor;
            senderText.gameObject.AddComponent<LayoutElement>().preferredWidth = 96f;

            var messageText = CreateText("Message", row, message, 14, FontStyles.Normal, TextAlignmentOptions.Left);
            var messageLayout = messageText.gameObject.AddComponent<LayoutElement>();
            messageLayout.flexibleWidth = 1f;

            if (chatMessageId > 0 && !isMine)
            {
                var reportButton = CreateButton("ReportButton", row, isReported ? "Reported" : "Report");
                reportButton.interactable = !isReported;
                reportButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 74f;
                reportButton.onClick.AddListener(() => ReportFriendMessage(chatMessageId, () =>
                {
                    reportButton.interactable = false;
                    var label = reportButton.GetComponentInChildren<TMP_Text>();
                    if (label != null) label.text = "Reported";
                }));
            }
        }

        private void HandleFriendReportClicked(UIChatMessage item)
        {
            if (item == null || item.ChatMessageId <= 0)
            {
                return;
            }

            ReportFriendMessage(item.ChatMessageId, item.MarkReported);
        }

        private void ReportFriendMessage(int chatMessageId, Action markReported)
        {
            if (pendingReportIds.Contains(chatMessageId))
            {
                return;
            }

            if (!ApiClient.Instance.HasToken())
            {
                AddSystemMessage("Please login before reporting chat.");
                return;
            }

            pendingReportIds.Add(chatMessageId);
            ChatApi.Instance.ReportFriendMessage(
                chatMessageId,
                "Reported from friend chat UI",
                response =>
                {
                    pendingReportIds.Remove(chatMessageId);
                    markReported?.Invoke();
                    Debug.Log($"[UIFriendChatPanel] ReportFriendMessage submitted. ChatMessageId={chatMessageId}");
                },
                error =>
                {
                    pendingReportIds.Remove(chatMessageId);
                    Debug.LogWarning($"[UIFriendChatPanel] ReportFriendMessage failed: {BuildErrorMessage(error)}");
                });
        }
        private void ClearMessages()
        {
            if (messageContainer == null)
            {
                return;
            }

            foreach (Transform child in messageContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void StartRefresh()
        {
            StopRefresh();
            refreshCoroutine = StartCoroutine(RefreshLoop());
        }

        private void StopRefresh()
        {
            if (refreshCoroutine == null)
            {
                return;
            }

            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }

        private IEnumerator RefreshLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(2f, refreshInterval));
            while (true)
            {
                yield return wait;
                LoadHistory();
            }
        }

        private void SetSending(bool sending)
        {
            isSending = sending;
            if (sendButton != null)
            {
                sendButton.interactable = !sending;
            }
        }

        private void SetStatus(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusText.text));
        }

        private void FocusInput()
        {
            if (inputField != null)
            {
                inputField.ActivateInputField();
            }
        }

        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();

            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private static bool IsCurrentPlayer(int profileId)
        {
            int currentPlayerId = GameStateService.Instance != null
                ? GameStateService.Instance.PlayerProfileId
                : 0;

            if (currentPlayerId <= 0)
            {
                currentPlayerId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
            }

            return currentPlayerId > 0 && profileId == currentPlayerId;
        }

        private string ResolveSenderName(FriendChatMessageResponse message, bool isMe)
        {
            if (isMe)
            {
                string playerName = GameStateService.Instance != null
                    ? GameStateService.Instance.PlayerName
                    : null;

                if (string.IsNullOrWhiteSpace(playerName))
                {
                    playerName = PlayerPrefs.GetString(ApiConfig.UserNameKey, "You");
                }

                return string.IsNullOrWhiteSpace(playerName) ? "You" : playerName;
            }

            if (!string.IsNullOrWhiteSpace(message.SenderName))
            {
                return message.SenderName;
            }

            return string.IsNullOrWhiteSpace(friendDisplayName)
                ? $"Player {message.SenderId}"
                : friendDisplayName;
        }

        private static string BuildErrorMessage(ApiException error)
        {
            if (error == null)
            {
                return "Cannot load friend chat.";
            }

            if (error.ErrorCode == "CHAT_LOCKED")
            {
                return string.IsNullOrWhiteSpace(error.Message)
                    ? "Chat is locked."
                    : error.Message;
            }

            if (error.StatusCode == 401)
            {
                return "Please login before using friend chat.";
            }

            return string.IsNullOrWhiteSpace(error.Message)
                ? "Cannot load friend chat."
                : error.Message;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = obj.GetComponent<RectTransform>();
            if (parent != null)
            {
                rect.SetParent(parent, false);
            }

            return rect;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, int size, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            var button = rect.gameObject.AddComponent<Button>();

            var text = CreateText("Label", rect, label, 14, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            return button;
        }

        private static TMP_InputField CreateInput(string name, Transform parent)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.11f, 0.13f, 0.16f, 1f);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;

            var textArea = CreateRect("Text Area", rect);
            SetRect(textArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(-20f, -8f));

            var placeholder = CreateText("Placeholder", textArea, "Message", 14, FontStyles.Italic, TextAlignmentOptions.Left);
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var inputText = CreateText("Text", textArea, string.Empty, 14, FontStyles.Normal, TextAlignmentOptions.Left);
            SetRect(inputText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            input.textViewport = textArea;
            input.textComponent = inputText;
            input.placeholder = placeholder;

            return input;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}