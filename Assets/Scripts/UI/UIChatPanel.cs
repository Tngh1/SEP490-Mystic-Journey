using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIChatPanel : MonoBehaviour
{
    [Header("Chat UI")]
    public ScrollRect scrollRect;
    public Transform contentParent;
    public TMP_InputField inputField;
    public Button sendButton;

    [Header("Message Prefab")]
    public UIChatMessage chatMessagePrefab;

    [Header("Context Menu (Optional)")]
    public UIPlayerContextMenu contextMenu;
    public UIReportConfirmPopup reportConfirmPopup;

    [Header("World Chat")]
    public bool loadHistoryOnEnable = true;
    public int historyPageSize = 50;
    public bool refreshHistoryWhenPhotonUnavailable = true;
    public float fallbackRefreshInterval = 5f;

    [Header("Colors")]
    public Color myNameColor = Color.yellow;
    public Color otherNameColor = Color.cyan;
    public Color systemNameColor = Color.gray;

    private readonly HashSet<int> displayedMessageIds = new HashSet<int>();
    private readonly HashSet<int> pendingReportIds = new HashSet<int>();
    private bool isSending;
    private bool isLoadingHistory;
    private Coroutine fallbackHistoryCoroutine;
    private WorldChatPhotonRelay subscribedRelay;

    [Header("Send Cooldown")]
    public float sendCooldownSeconds = 10f;
    private Coroutine sendCooldownCoroutine;
    private string sendButtonOriginalLabel;
    private bool isOnCooldown;

    private void OnEnable()
    {
        SubscribePhotonRelay();

        if (loadHistoryOnEnable)
        {
            Debug.Log("[UIChatPanel] OnEnable -> LoadWorldHistory()");
            LoadWorldHistory();
        }

        UpdateHistoryFallbackState();
    }

    private void Start()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendClicked);
        }

        if (inputField != null)
        {
            inputField.onSubmit.AddListener((text) => OnSendClicked());
        }

        SubscribePhotonRelay();
        UpdateHistoryFallbackState();
    }

    private void Update()
    {
        if (subscribedRelay == null && WorldChatPhotonRelay.Instance != null)
        {
            SubscribePhotonRelay();
        }

        UpdateHistoryFallbackState();
    }

    private void OnDisable()
    {
        StopHistoryFallback();
        UnsubscribePhotonRelay();
    }

    public void OnSendClicked()
    {
        if (isSending || isOnCooldown || inputField == null)
        {
            return;
        }

        string msg = inputField.text != null ? inputField.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(msg))
        {
            return;
        }

        if (!ApiClient.Instance.HasToken())
        {
            AddSystemMessage("Please login before using world chat.");
            FocusInput();
            return;
        }

        inputField.text = string.Empty;
        SetSending(true);

        ChatApi.Instance.SendWorldMessage(
            msg,
            message =>
            {
                SetSending(false);
                StartSendCooldown();
                AddWorldMessage(message);

                var relay = WorldChatPhotonRelay.Instance;
                if (relay != null)
                {
                    relay.BroadcastWorldMessage(message);
                }

                UpdateHistoryFallbackState();
                FocusInput();
            },
            error =>
            {
                SetSending(false);
                inputField.text = msg;
                AddSystemMessage(BuildErrorMessage(error));
                FocusInput();
            });
    }

    public void LoadWorldHistory()
    {
        if (isLoadingHistory || !isActiveAndEnabled)
        {
            Debug.Log($"[UIChatPanel] LoadWorldHistory SKIP: isLoadingHistory={isLoadingHistory} isActiveAndEnabled={isActiveAndEnabled}");
            return;
        }

        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[UIChatPanel] LoadWorldHistory SKIP: No auth token.");
            return;
        }

        isLoadingHistory = true;
        int safePageSize = Mathf.Clamp(historyPageSize, 1, 100);
        Debug.Log($"[UIChatPanel] LoadWorldHistory -> requesting page=1 pageSize={safePageSize}");

        ChatApi.Instance.GetWorldMessages(
            1,
            safePageSize,
            response =>
            {
                isLoadingHistory = false;
                Debug.Log($"[UIChatPanel] GetWorldMessages success: TotalCount={response?.TotalCount ?? 0} Items={response?.Items?.Length ?? 0}");
                PopulateWorldHistory(response);
            },
            error =>
            {
                isLoadingHistory = false;
                Debug.LogWarning($"[UIChatPanel] Load world chat history failed: {error}");
            });
    }

    public void AddWorldMessage(WorldChatMessageResponse message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content) || message.IsHidden)
        {
            return;
        }

        if (message.ChatMessageId > 0 && !displayedMessageIds.Add(message.ChatMessageId))
        {
            return;
        }

        bool isMe = IsCurrentPlayer(message.SenderId);
        string sender = ResolveSenderName(message, isMe);
        Color senderColor = isMe ? myNameColor : otherNameColor;
        AddMessage(sender, message.Content, senderColor, message.ChatMessageId, message.SenderId, isMe, message.IsReported);
    }


    public void AddMessage(string sender, string message, bool isMe)
    {
        Color senderColor = isMe ? myNameColor : otherNameColor;
        AddMessage(sender, message, senderColor, 0, 0, isMe, false);
    }

    private void AddSystemMessage(string message)
    {
        AddMessage("System", message, systemNameColor);
    }

    private void AddMessage(string sender, string message, Color senderColor, int chatMessageId = 0, int senderProfileId = 0, bool isMine = true, bool isReported = false)
    {
        if (chatMessagePrefab == null || contentParent == null)
        {
            Debug.LogError($"[UIChatPanel] AddMessage SKIP: chatMessagePrefab={chatMessagePrefab} contentParent={contentParent}");
            return;
        }

        UIChatMessage newMsg = Instantiate(chatMessagePrefab, contentParent);
        // Force active in case the prefab asset root is disabled
        newMsg.gameObject.SetActive(true);
        newMsg.Setup(sender, message, senderColor, new Color(0, 0, 0, 0), chatMessageId, senderProfileId, isMine, isReported);

        newMsg.OnSenderClicked += HandleSenderNameClicked;
        newMsg.OnReportClicked += HandleWorldReportClicked;

        StartCoroutine(ScrollToBottom());
    }

    private void HandleSenderNameClicked(string senderName, int senderProfileId, Vector3 clickPosition)
    {
        // Không mở menu cho tin nhắn của chính mình
        if (IsCurrentPlayer(senderProfileId))
            return;

        // Bỏ qua nếu không có profileId (tin nhắn hệ thống, v.v.)
        if (senderProfileId <= 0)
            return;

        if (contextMenu != null)
        {
            contextMenu.ShowMenu(senderName, senderProfileId, clickPosition);
        }
        else
        {
            Debug.LogError("[UIChatPanel] CHƯA KÉO PLAYER CONTEXT MENU VÀO TRONG INSPECTOR!");
        }
    }

    private void HandleWorldReportClicked(UIChatMessage item)
    {
        if (item == null || item.ChatMessageId <= 0 || pendingReportIds.Contains(item.ChatMessageId))
        {
            return;
        }

        if (!ApiClient.Instance.HasToken())
        {
            AddSystemMessage("Please login before reporting chat.");
            return;
        }

        if (reportConfirmPopup != null)
        {
            reportConfirmPopup.ShowPopup("this message", () => ExecuteReport(item));
        }
        else
        {
            ExecuteReport(item);
        }
    }

    private void ExecuteReport(UIChatMessage item)
    {
        pendingReportIds.Add(item.ChatMessageId);
        ChatApi.Instance.ReportWorldMessage(
            item.ChatMessageId,
            "Reported from world chat UI",
            response =>
            {
                pendingReportIds.Remove(item.ChatMessageId);
                item.MarkReported();
                Debug.Log($"[UIChatPanel] ReportWorldMessage submitted. ChatMessageId={item.ChatMessageId}");
            },
            error =>
            {
                pendingReportIds.Remove(item.ChatMessageId);
                Debug.LogWarning($"[UIChatPanel] ReportWorldMessage failed: {BuildErrorMessage(error)}");
            });
    }

    private void PopulateWorldHistory(PagedResultResponse<WorldChatMessageResponse> response)
    {
        if (response == null || response.Items == null)
        {
            Debug.LogWarning("[UIChatPanel] PopulateWorldHistory: response or Items is null.");
            return;
        }

        Debug.Log($"[UIChatPanel] PopulateWorldHistory: {response.Items.Length} messages");
        foreach (var message in response.Items)
        {
            AddWorldMessage(message);
        }
    }

    private void SubscribePhotonRelay()
    {
        var relay = WorldChatPhotonRelay.Instance;
        if (relay == null || subscribedRelay == relay)
        {
            return;
        }

        UnsubscribePhotonRelay();
        subscribedRelay = relay;
        subscribedRelay.WorldMessageReceived += OnPhotonWorldMessageReceived;
    }

    private void UnsubscribePhotonRelay()
    {
        if (subscribedRelay == null)
        {
            return;
        }

        subscribedRelay.WorldMessageReceived -= OnPhotonWorldMessageReceived;
        subscribedRelay = null;
    }

    private void UpdateHistoryFallbackState()
    {
        bool shouldRefresh = refreshHistoryWhenPhotonUnavailable &&
            isActiveAndEnabled &&
            ApiClient.Instance.HasToken() &&
            !HasReadyPhotonRelay();

        if (shouldRefresh)
        {
            StartHistoryFallback();
        }
        else
        {
            StopHistoryFallback();
        }
    }

    private bool HasReadyPhotonRelay()
    {
        var relay = subscribedRelay != null ? subscribedRelay : WorldChatPhotonRelay.Instance;
        return relay != null && relay.IsReady;
    }

    private void StartHistoryFallback()
    {
        if (fallbackHistoryCoroutine != null)
        {
            return;
        }

        fallbackHistoryCoroutine = StartCoroutine(RefreshHistoryWithoutPhoton());
    }

    private void StopHistoryFallback()
    {
        if (fallbackHistoryCoroutine == null)
        {
            return;
        }

        StopCoroutine(fallbackHistoryCoroutine);
        fallbackHistoryCoroutine = null;
    }

    private IEnumerator RefreshHistoryWithoutPhoton()
    {
        var wait = new WaitForSeconds(Mathf.Max(2f, fallbackRefreshInterval));

        while (true)
        {
            yield return wait;

            if (!HasReadyPhotonRelay())
            {
                LoadWorldHistory();
            }
        }
    }

    private void OnPhotonWorldMessageReceived(WorldChatMessageResponse message)
    {
        AddWorldMessage(message);
    }

    private static bool IsCurrentPlayer(int senderId)
    {
        int currentPlayerId = GameStateService.Instance != null
            ? GameStateService.Instance.PlayerProfileId
            : 0;

        if (currentPlayerId <= 0)
        {
            currentPlayerId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
        }

        return currentPlayerId > 0 && senderId == currentPlayerId;
    }

    private static string ResolveSenderName(WorldChatMessageResponse message, bool isMe)
    {
        if (!string.IsNullOrWhiteSpace(message.SenderName))
        {
            return message.SenderName;
        }

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

        return message.SenderId > 0 ? $"Player {message.SenderId}" : "Player";
    }

    private static string BuildErrorMessage(ApiException error)
    {
        if (error == null)
        {
            return "Cannot send chat message.";
        }

        if (error.ErrorCode == "RATE_LIMITED")
        {
            return string.IsNullOrWhiteSpace(error.Message)
                ? "Please wait before sending another world message."
                : error.Message;
        }

        if (error.StatusCode == 401)
        {
            return "Please login before using world chat.";
        }

        return string.IsNullOrWhiteSpace(error.Message)
            ? "Cannot send chat message."
            : error.Message;
    }

    private void SetSending(bool sending)
    {
        isSending = sending;

        if (sendButton != null && !isOnCooldown)
        {
            sendButton.interactable = !sending;
        }
    }

    private void StartSendCooldown()
    {
        if (sendCooldownCoroutine != null)
        {
            StopCoroutine(sendCooldownCoroutine);
        }

        sendCooldownCoroutine = StartCoroutine(SendCooldownRoutine());
    }

    private IEnumerator SendCooldownRoutine()
    {
        isOnCooldown = true;

        // Cache and replace button label
        TMP_Text buttonLabel = sendButton != null
            ? sendButton.GetComponentInChildren<TMP_Text>()
            : null;

        if (buttonLabel != null && string.IsNullOrEmpty(sendButtonOriginalLabel))
        {
            sendButtonOriginalLabel = buttonLabel.text;
        }

        if (sendButton != null)
        {
            sendButton.interactable = false;
        }

        float remaining = sendCooldownSeconds;
        while (remaining > 0f)
        {
            if (buttonLabel != null)
            {
                buttonLabel.text = Mathf.CeilToInt(remaining).ToString();
            }

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        // Restore
        isOnCooldown = false;
        sendCooldownCoroutine = null;

        if (buttonLabel != null)
        {
            buttonLabel.text = !string.IsNullOrEmpty(sendButtonOriginalLabel)
                ? sendButtonOriginalLabel
                : "Send";
        }

        if (sendButton != null)
        {
            sendButton.interactable = true;
        }
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

        // Force layout rebuild so ContentSizeFitter recalculates Content height
        if (contentParent is RectTransform contentRt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        }

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
