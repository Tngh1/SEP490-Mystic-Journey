using System;
using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIChatPanel : MonoBehaviour
{
    private enum ChatChannel
    {
        World,
        Guild,
        Party
    }

    [Header("Chat UI")]
    public ScrollRect scrollRect;
    public Transform contentParent;
    public TMP_InputField inputField;
    public Button sendButton;

    [Header("Channel Tabs")]
    public Button worldTabButton;
    public Button guildTabButton;
    public Button partyTabButton;
    public bool autoFindChannelTabs = true;

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

    [Header("Guild Chat")]
    public float guildRefreshInterval = 5f;

    [Header("Party Chat")]
    public bool clearPartyChatOnPartyChanged = true;

    [Header("Colors")]
    public Color myNameColor = Color.yellow;
    public Color otherNameColor = Color.cyan;
    public Color systemNameColor = Color.gray;

    [Header("Send Cooldown")]
    public float sendCooldownSeconds = 10f;

    private readonly HashSet<int> displayedMessageIds = new HashSet<int>();
    private readonly HashSet<int> pendingReportIds = new HashSet<int>();
    private readonly HashSet<string> displayedRealtimeKeys = new HashSet<string>();

    private ChatChannel currentChannel = ChatChannel.World;
    private bool isSending;
    private bool isLoadingWorldHistory;
    private bool isLoadingGuildHistory;
    private int currentGuildId;

    private Coroutine fallbackHistoryCoroutine;
    private Coroutine guildRefreshCoroutine;
    private WorldChatPhotonRelay subscribedRelay;
    private PartyLobby subscribedParty;

    private Coroutine sendCooldownCoroutine;
    private string sendButtonOriginalLabel;
    private bool isOnCooldown;

    private bool sendEventsBound;
    private bool worldTabBound;
    private bool guildTabBound;
    private bool partyTabBound;
    private bool partyStaticEventBound;

    private void OnEnable()
    {
        PrepareRuntimeBindings();
        SubscribeChannelRelays();

        if (loadHistoryOnEnable)
        {
            LoadCurrentChannelHistory();
        }

        UpdateHistoryFallbackState();
        RefreshSendButtonState();
    }

    private void Start()
    {
        PrepareRuntimeBindings();
        SubscribeChannelRelays();
        UpdateHistoryFallbackState();
        RefreshSendButtonState();
    }

    private void Update()
    {
        if (currentChannel == ChatChannel.World && subscribedRelay == null && WorldChatPhotonRelay.Instance != null)
        {
            SubscribePhotonRelay();
        }

        if (currentChannel == ChatChannel.Party && subscribedParty != PartyLobby.Local)
        {
            SubscribePartyLobby();
        }

        UpdateHistoryFallbackState();
    }

    private void OnDisable()
    {
        StopHistoryFallback();
        StopGuildRefresh();
        UnsubscribePhotonRelay();
        UnsubscribePartyLobby();
        UnbindPartyStaticEvent();
    }

    public void ShowWorldChat()
    {
        SwitchChannel(ChatChannel.World);
    }

    public void ShowGuildChat()
    {
        SwitchChannel(ChatChannel.Guild);
    }

    public void ShowPartyChat()
    {
        SwitchChannel(ChatChannel.Party);
    }

    public void OnSendClicked()
    {
        if (isSending || inputField == null)
        {
            return;
        }

        if (currentChannel == ChatChannel.World && isOnCooldown)
        {
            return;
        }

        string msg = inputField.text != null ? inputField.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(msg))
        {
            return;
        }

        switch (currentChannel)
        {
            case ChatChannel.World:
                SendWorldMessage(msg);
                break;
            case ChatChannel.Guild:
                SendGuildMessage(msg);
                break;
            case ChatChannel.Party:
                SendPartyMessage(msg);
                break;
        }
    }

    public void LoadWorldHistory()
    {
        if (isLoadingWorldHistory || !isActiveAndEnabled)
        {
            Debug.Log($"[UIChatPanel] LoadWorldHistory SKIP: isLoading={isLoadingWorldHistory} isActiveAndEnabled={isActiveAndEnabled}");
            return;
        }

        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[UIChatPanel] LoadWorldHistory SKIP: No auth token.");
            return;
        }

        isLoadingWorldHistory = true;
        int safePageSize = Mathf.Clamp(historyPageSize, 1, 100);
        Debug.Log($"[UIChatPanel] LoadWorldHistory -> requesting page=1 pageSize={safePageSize}");

        ChatApi.Instance.GetWorldMessages(
            1,
            safePageSize,
            response =>
            {
                isLoadingWorldHistory = false;
                Debug.Log($"[UIChatPanel] GetWorldMessages success: TotalCount={response?.TotalCount ?? 0} Items={response?.Items?.Length ?? 0}");
                PopulateWorldHistory(response);
            },
            error =>
            {
                isLoadingWorldHistory = false;
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
        string sender = ResolveSenderName(message.SenderId, message.SenderName, isMe);
        Color senderColor = isMe ? myNameColor : otherNameColor;
        AddMessage(sender, message.Content, senderColor, message.ChatMessageId, message.SenderId, isMe, message.IsReported);
    }

    public void AddMessage(string sender, string message, bool isMe)
    {
        Color senderColor = isMe ? myNameColor : otherNameColor;
        AddMessage(sender, message, senderColor, 0, 0, isMe, false);
    }

    private void PrepareRuntimeBindings()
    {
        AutoFindChannelTabs();
        BindSendEvents();
        BindChannelTabs();
        BindPartyStaticEvent();
    }

    private void BindSendEvents()
    {
        if (sendEventsBound)
        {
            return;
        }

        sendEventsBound = true;

        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendClicked);
        }

        if (inputField != null)
        {
            inputField.onSubmit.AddListener(HandleInputSubmitted);
        }
    }

    private void HandleInputSubmitted(string _)
    {
        OnSendClicked();
    }

    private void AutoFindChannelTabs()
    {
        if (!autoFindChannelTabs)
        {
            return;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            string label = GetButtonLabel(button);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            string normalized = label.Trim().ToLowerInvariant();
            if (worldTabButton == null && normalized.Contains("world"))
            {
                worldTabButton = button;
            }
            else if (guildTabButton == null && normalized.Contains("guild"))
            {
                guildTabButton = button;
            }
            else if (partyTabButton == null && (normalized.Contains("party") || normalized.Contains("team")))
            {
                partyTabButton = button;
            }
        }
    }

    private static string GetButtonLabel(Button button)
    {
        if (button == null)
        {
            return string.Empty;
        }

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            return tmp.text;
        }

        Text legacyText = button.GetComponentInChildren<Text>(true);
        return legacyText != null ? legacyText.text : string.Empty;
    }

    private void BindChannelTabs()
    {
        if (worldTabButton != null && !worldTabBound)
        {
            worldTabBound = true;
            worldTabButton.onClick.AddListener(ShowWorldChat);
        }

        if (guildTabButton != null && !guildTabBound)
        {
            guildTabBound = true;
            guildTabButton.onClick.AddListener(ShowGuildChat);
        }

        if (partyTabButton != null && !partyTabBound)
        {
            partyTabBound = true;
            partyTabButton.onClick.AddListener(ShowPartyChat);
        }
    }

    private void BindPartyStaticEvent()
    {
        if (partyStaticEventBound)
        {
            return;
        }

        partyStaticEventBound = true;
        PartyLobby.OnLocalPartyChanged += HandleLocalPartyChanged;
    }

    private void UnbindPartyStaticEvent()
    {
        if (!partyStaticEventBound)
        {
            return;
        }

        partyStaticEventBound = false;
        PartyLobby.OnLocalPartyChanged -= HandleLocalPartyChanged;
    }

    private void SwitchChannel(ChatChannel channel)
    {
        if (currentChannel == channel && contentParent != null && contentParent.childCount > 0)
        {
            return;
        }

        currentChannel = channel;
        ClearMessages();
        StopHistoryFallback();
        StopGuildRefresh();
        SubscribeChannelRelays();
        LoadCurrentChannelHistory();
        UpdateHistoryFallbackState();
        RefreshSendButtonState();
        FocusInput();
    }

    private void LoadCurrentChannelHistory()
    {
        switch (currentChannel)
        {
            case ChatChannel.World:
                LoadWorldHistory();
                break;
            case ChatChannel.Guild:
                LoadGuildHistoryWithResolvedGuild();
                StartGuildRefresh();
                break;
            case ChatChannel.Party:
                SubscribePartyLobby();
                if (PartyLobby.Local == null)
                {
                    AddSystemMessage("You are not in a party.");
                }
                break;
        }
    }

    private void SubscribeChannelRelays()
    {
        if (currentChannel == ChatChannel.World)
        {
            SubscribePhotonRelay();
        }
        else
        {
            UnsubscribePhotonRelay();
        }

        if (currentChannel == ChatChannel.Party)
        {
            SubscribePartyLobby();
        }
        else
        {
            UnsubscribePartyLobby();
        }
    }

    private void SendWorldMessage(string msg)
    {
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

    private void SendGuildMessage(string msg)
    {
        if (!ApiClient.Instance.HasToken())
        {
            AddSystemMessage("Please login before using guild chat.");
            FocusInput();
            return;
        }

        inputField.text = string.Empty;
        SetSending(true);

        ResolveGuildId(
            guildId =>
            {
                GuildApi.SendChat(
                    guildId,
                    msg,
                    message =>
                    {
                        SetSending(false);
                        AddGuildMessage(message);
                        FocusInput();
                    },
                    error =>
                    {
                        SetSending(false);
                        inputField.text = msg;
                        AddSystemMessage(BuildErrorMessage(error));
                        FocusInput();
                    });
            },
            errorMessage =>
            {
                SetSending(false);
                inputField.text = msg;
                AddSystemMessage(errorMessage);
                FocusInput();
            });
    }

    private void SendPartyMessage(string msg)
    {
        var party = PartyLobby.Local;
        if (party == null)
        {
            AddSystemMessage("You are not in a party.");
            FocusInput();
            return;
        }

        int senderId = GetCurrentPlayerProfileId();
        if (senderId <= 0)
        {
            AddSystemMessage("Cannot resolve your player profile.");
            FocusInput();
            return;
        }

        inputField.text = string.Empty;

        var message = new PartyChatMessageResponse
        {
            SenderId = senderId,
            SenderName = GetCurrentPlayerName(),
            Content = msg,
            Channel = "Party",
            SentAt = DateTime.UtcNow.ToString("O")
        };

        if (!party.BroadcastPartyMessage(message))
        {
            inputField.text = msg;
            AddSystemMessage("Party chat is not ready.");
        }

        FocusInput();
    }

    private void LoadGuildHistoryWithResolvedGuild()
    {
        if (!ApiClient.Instance.HasToken())
        {
            AddSystemMessage("Please login before using guild chat.");
            return;
        }

        ResolveGuildId(
            LoadGuildHistory,
            errorMessage => AddSystemMessage(errorMessage));
    }

    private void LoadGuildHistory(int guildId)
    {
        if (isLoadingGuildHistory || !isActiveAndEnabled || currentChannel != ChatChannel.Guild)
        {
            return;
        }

        isLoadingGuildHistory = true;
        GuildApi.GetChat(
            guildId,
            messages =>
            {
                isLoadingGuildHistory = false;
                if (messages == null)
                {
                    return;
                }

                foreach (GuildMessageDTO message in messages)
                {
                    AddGuildMessage(message);
                }
            },
            error =>
            {
                isLoadingGuildHistory = false;
                Debug.LogWarning($"[UIChatPanel] Load guild chat failed: {BuildErrorMessage(error)}");
            });
    }

    private void ResolveGuildId(Action<int> onResolved, Action<string> onFailed)
    {
        if (currentGuildId > 0)
        {
            onResolved?.Invoke(currentGuildId);
            return;
        }

        GuildApi.GetMyGuild(
            detail =>
            {
                currentGuildId = detail != null ? detail.guildId : 0;

                if (currentGuildId > 0)
                {
                    onResolved?.Invoke(currentGuildId);
                }
                else
                {
                    onFailed?.Invoke("You are not in a guild.");
                }
            },
            error =>
            {
                currentGuildId = 0;
                onFailed?.Invoke(BuildErrorMessage(error));
            });
    }

    private void AddGuildMessage(GuildMessageDTO message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.content))
        {
            return;
        }

        if (message.messageId > 0 && !displayedMessageIds.Add(message.messageId))
        {
            return;
        }

        bool isMe = IsCurrentPlayer(message.senderId);
        string sender = ResolveSenderName(message.senderId, message.senderName, isMe);
        Color senderColor = isMe ? myNameColor : otherNameColor;
        AddMessage(sender, message.content, senderColor, 0, message.senderId, isMe, false);
    }

    private void AddPartyMessage(PartyChatMessageResponse message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content))
        {
            return;
        }

        string key = $"{message.SenderId}|{message.SentAt}|{message.Content}";
        if (!displayedRealtimeKeys.Add(key))
        {
            return;
        }

        bool isMe = IsCurrentPlayer(message.SenderId);
        string sender = ResolveSenderName(message.SenderId, message.SenderName, isMe);
        Color senderColor = isMe ? myNameColor : otherNameColor;
        AddMessage(sender, message.Content, senderColor, 0, message.SenderId, isMe, false);
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
        newMsg.gameObject.SetActive(true);
        newMsg.Setup(sender, message, senderColor, new Color(0, 0, 0, 0), chatMessageId, senderProfileId, isMine, isReported);

        newMsg.OnSenderClicked += HandleSenderNameClicked;
        newMsg.OnReportClicked += HandleWorldReportClicked;

        StartCoroutine(ScrollToBottom());
    }

    private void ClearMessages()
    {
        displayedMessageIds.Clear();
        pendingReportIds.Clear();
        displayedRealtimeKeys.Clear();

        if (contentParent == null)
        {
            return;
        }

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }
    }

    private void HandleSenderNameClicked(string senderName, int senderProfileId, Vector3 clickPosition)
    {
        if (IsCurrentPlayer(senderProfileId) || senderProfileId <= 0)
        {
            return;
        }

        if (contextMenu != null)
        {
            contextMenu.ShowMenu(senderName, senderProfileId, clickPosition);
        }
        else
        {
            Debug.LogError("[UIChatPanel] Player context menu is not assigned in Inspector.");
        }
    }

    private void HandleWorldReportClicked(UIChatMessage item)
    {
        if (currentChannel != ChatChannel.World || item == null || item.ChatMessageId <= 0 || pendingReportIds.Contains(item.ChatMessageId))
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

    private void SubscribePartyLobby()
    {
        var party = PartyLobby.Local;
        if (subscribedParty == party)
        {
            return;
        }

        UnsubscribePartyLobby();
        subscribedParty = party;

        if (subscribedParty != null)
        {
            subscribedParty.PartyMessageReceived += OnPartyMessageReceived;
        }
    }

    private void UnsubscribePartyLobby()
    {
        if (subscribedParty == null)
        {
            return;
        }

        subscribedParty.PartyMessageReceived -= OnPartyMessageReceived;
        subscribedParty = null;
    }

    private void HandleLocalPartyChanged()
    {
        if (currentChannel != ChatChannel.Party)
        {
            return;
        }

        var previous = subscribedParty;
        SubscribePartyLobby();

        if (clearPartyChatOnPartyChanged && previous != subscribedParty)
        {
            ClearMessages();
            if (subscribedParty == null)
            {
                AddSystemMessage("You are not in a party.");
            }
        }
    }

    private void UpdateHistoryFallbackState()
    {
        bool shouldRefresh = currentChannel == ChatChannel.World &&
            refreshHistoryWhenPhotonUnavailable &&
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

            if (currentChannel == ChatChannel.World && !HasReadyPhotonRelay())
            {
                LoadWorldHistory();
            }
        }
    }

    private void StartGuildRefresh()
    {
        if (guildRefreshCoroutine != null)
        {
            return;
        }

        guildRefreshCoroutine = StartCoroutine(RefreshGuildChat());
    }

    private void StopGuildRefresh()
    {
        if (guildRefreshCoroutine == null)
        {
            return;
        }

        StopCoroutine(guildRefreshCoroutine);
        guildRefreshCoroutine = null;
    }

    private IEnumerator RefreshGuildChat()
    {
        var wait = new WaitForSeconds(Mathf.Max(2f, guildRefreshInterval));

        while (true)
        {
            yield return wait;

            if (currentChannel == ChatChannel.Guild && currentGuildId > 0)
            {
                LoadGuildHistory(currentGuildId);
            }
        }
    }

    private void OnPhotonWorldMessageReceived(WorldChatMessageResponse message)
    {
        if (currentChannel == ChatChannel.World)
        {
            AddWorldMessage(message);
        }
    }

    private void OnPartyMessageReceived(PartyChatMessageResponse message)
    {
        if (currentChannel == ChatChannel.Party)
        {
            AddPartyMessage(message);
        }
    }

    private static bool IsCurrentPlayer(int senderId)
    {
        int currentPlayerId = GetCurrentPlayerProfileId();
        return currentPlayerId > 0 && senderId == currentPlayerId;
    }

    private static int GetCurrentPlayerProfileId()
    {
        int currentPlayerId = GameStateService.Instance != null
            ? GameStateService.Instance.PlayerProfileId
            : 0;

        if (currentPlayerId <= 0)
        {
            currentPlayerId = WorldState.PlayerProfileId;
        }

        if (currentPlayerId <= 0)
        {
            currentPlayerId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
        }

        return currentPlayerId;
    }

    private static string GetCurrentPlayerName()
    {
        string playerName = GameStateService.Instance != null
            ? GameStateService.Instance.PlayerName
            : null;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = WorldState.PlayerName;
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = PlayerPrefs.GetString(ApiConfig.UserNameKey, "You");
        }

        return string.IsNullOrWhiteSpace(playerName) ? "You" : playerName;
    }

    private static string ResolveSenderName(int senderId, string senderName, bool isMe)
    {
        if (!string.IsNullOrWhiteSpace(senderName))
        {
            return senderName;
        }

        if (isMe)
        {
            return GetCurrentPlayerName();
        }

        return senderId > 0 ? $"Player {senderId}" : "Player";
    }

    private static string BuildErrorMessage(ApiException error)
    {
        if (error == null)
        {
            return "Cannot send chat message.";
        }

        if (error.ErrorCode == "RATE_LIMITED" || error.StatusCode == 429)
        {
            return string.IsNullOrWhiteSpace(error.Message)
                ? "Please wait before sending another chat message."
                : error.Message;
        }

        if (error.StatusCode == 401)
        {
            return "Please login before using chat.";
        }

        if (error.StatusCode == 403)
        {
            return "You are not allowed to use this chat.";
        }

        if (error.StatusCode == 404)
        {
            return "Chat target not found.";
        }

        return string.IsNullOrWhiteSpace(error.Message)
            ? "Cannot send chat message."
            : error.Message;
    }

    private void SetSending(bool sending)
    {
        isSending = sending;
        RefreshSendButtonState();
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

        TMP_Text buttonLabel = GetSendButtonLabel();
        if (buttonLabel != null && string.IsNullOrEmpty(sendButtonOriginalLabel))
        {
            sendButtonOriginalLabel = buttonLabel.text;
        }

        float remaining = sendCooldownSeconds;
        while (remaining > 0f)
        {
            if (currentChannel == ChatChannel.World)
            {
                if (buttonLabel != null)
                {
                    buttonLabel.text = Mathf.CeilToInt(remaining).ToString();
                }
                if (sendButton != null)
                {
                    sendButton.interactable = false;
                }
            }
            else
            {
                RestoreSendButtonLabel();
                if (sendButton != null && !isSending)
                {
                    sendButton.interactable = true;
                }
            }

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        isOnCooldown = false;
        sendCooldownCoroutine = null;
        RestoreSendButtonLabel();
        RefreshSendButtonState();
    }

    private TMP_Text GetSendButtonLabel()
    {
        return sendButton != null ? sendButton.GetComponentInChildren<TMP_Text>() : null;
    }

    private void RestoreSendButtonLabel()
    {
        TMP_Text buttonLabel = GetSendButtonLabel();
        if (buttonLabel != null)
        {
            buttonLabel.text = !string.IsNullOrEmpty(sendButtonOriginalLabel)
                ? sendButtonOriginalLabel
                : "Send";
        }
    }

    private void RefreshSendButtonState()
    {
        if (sendButton == null)
        {
            return;
        }

        sendButton.interactable = !isSending && (currentChannel != ChatChannel.World || !isOnCooldown);

        if (currentChannel != ChatChannel.World || !isOnCooldown)
        {
            RestoreSendButtonLabel();
        }
    }

    private IEnumerator ScrollToBottom()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void FocusInput()
    {
        if (inputField == null)
        {
            return;
        }

        inputField.ActivateInputField();
        inputField.Select();
    }
}
