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

// Executes core business logic for mono behaviour.
public class ChatUIManager : MonoBehaviour
{
    // Executes core business logic for chat channel.
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

    private const int MaxPendingPartyMessages = 50;
    private readonly Queue<PartyChatMessageResponse> pendingPartyMessages = new Queue<PartyChatMessageResponse>();

    private ChatChannel currentChannel = ChatChannel.World;
    private bool isSending;
    private bool isLoadingWorldHistory;
    private bool isLoadingGuildHistory;
    private int currentGuildId;

    private Coroutine fallbackHistoryCoroutine;
    private Coroutine guildRefreshCoroutine;
    private bool worldRelayBound;
    private PartyLobby subscribedParty;

    private Coroutine sendCooldownCoroutine;
    private string sendButtonOriginalLabel;
    private bool isOnCooldown;

    private bool sendEventsBound;
    private bool worldTabBound;
    private bool guildTabBound;
    private bool partyTabBound;
    private bool partyStaticEventBound;
    private bool partyNetworkEventBound;

    private float enableTime;

    // Subscribes event handlers, initializes channel listeners, and loads chat history.
    private void OnEnable()
    {
        enableTime = Time.unscaledTime; // Cache enable timestamp
        PrepareRuntimeBindings(); // Hook button listeners and input fields
        SubscribeChannelRelays(); // Bind Photon realtime RPC events and party relays

        if (loadHistoryOnEnable)
        {
            LoadCurrentChannelHistory(); // Query previous messages for active channel tab
        }

        UpdateHistoryFallbackState();
        RefreshSendButtonState();
    }

    // Performs startup initialization for ChatUIManager on the first active frame.
    private void Start()
    {
        PrepareRuntimeBindings();
        SubscribeChannelRelays();
        UpdateHistoryFallbackState();
        RefreshSendButtonState();
    }

    // Handles per-frame transport updates and click-outside window dismissal.
    private void Update()
    {
        if (currentChannel == ChatChannel.Party &&
            (!partyNetworkEventBound || subscribedParty != PartyLobby.Local))
        {
            SubscribePartyTransport(); // Reconnect party network stream if local player switched parties
        }

        UpdateHistoryFallbackState();
        CheckClickOutside(); // Dismiss chat overlay if user clicks elsewhere
    }

    // Dismisses chat modal if the user clicks outside its UI bounds.
    private void CheckClickOutside()
    {
        if (Time.unscaledTime - enableTime < 0.15f)
            return; // Debounce immediate click on open frame

        if (Input.GetMouseButtonDown(0))
        {
            RectTransform rect = GetComponent<RectTransform>();
            if (rect == null) return;

            Vector2 mousePos = Input.mousePosition;
            Camera cam = null;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos, cam))
            {
                if (contextMenu != null && contextMenu.gameObject.activeInHierarchy)
                {
                    RectTransform ctxRect = contextMenu.GetComponent<RectTransform>();
                    if (ctxRect != null && RectTransformUtility.RectangleContainsScreenPoint(ctxRect, mousePos, cam))
                        return; // Ignore clicks inside active context menu popup
                }

                if (reportConfirmPopup != null && reportConfirmPopup.gameObject.activeInHierarchy)
                {
                    RectTransform rptRect = reportConfirmPopup.GetComponent<RectTransform>();
                    if (rptRect != null && RectTransformUtility.RectangleContainsScreenPoint(rptRect, mousePos, cam))
                        return; // Ignore clicks inside active report confirmation modal
                }

                gameObject.SetActive(false); // Close chat UI
            }
        }
    }

    // Unsubscribes events and stops background polling coroutines when chat is hidden.
    private void OnDisable()
    {
        CancelSendCooldown();
        StopHistoryFallback(); // Stop HTTP fallback polling
        StopGuildRefresh(); // Stop guild chat polling
        UnsubscribePhotonRelay(); // Unbind Photon RPC event
        UnsubscribePartyTransport();
        UnbindPartyStaticEvent();
    }

    // Switches active tab to World Chat.
    public void ShowWorldChat()
    {
        SwitchChannel(ChatChannel.World); // Activate world chat view
    }

    // Switches active tab to Guild Chat.
    public void ShowGuildChat()
    {
        SwitchChannel(ChatChannel.Guild); // Activate guild chat view
    }

    // Switches active tab to Party Chat.
    public void ShowPartyChat()
    {
        SwitchChannel(ChatChannel.Party); // Activate party chat view
    }

    // Submits typed message to active channel (World, Guild, Party) via REST or Photon RPC.
    public void OnSendClicked()
    {
        if (isSending || inputField == null)
        {
            return; // Ignore if already awaiting response
        }

        if (currentChannel == ChatChannel.World && isOnCooldown)
        {
            AddSystemMessage("Please wait before sending another world message."); // Anti-spam warning
            FocusInput();
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

    // Executes core business logic for load world history.
    public void LoadWorldHistory()
    {
        if (isLoadingWorldHistory || !isActiveAndEnabled)
        {
            Debug.Log($"[ChatUIManager] LoadWorldHistory SKIP: isLoading={isLoadingWorldHistory} isActiveAndEnabled={isActiveAndEnabled}");
            return;
        }

        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[ChatUIManager] LoadWorldHistory SKIP: No auth token.");
            return;
        }

        isLoadingWorldHistory = true;
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        int safePageSize = Mathf.Clamp(historyPageSize, 1, 100);
        Debug.Log($"[ChatUIManager] LoadWorldHistory -> requesting page=1 pageSize={safePageSize}");

        ChatApi.Instance.GetWorldMessages(
            1,
            safePageSize,
            response =>
            {
                isLoadingWorldHistory = false;
                Debug.Log($"[ChatUIManager] GetWorldMessages success: TotalCount={response?.TotalCount ?? 0} Items={response?.Items?.Length ?? 0}");
                PopulateWorldHistory(response);
            },
            error =>
            {
                isLoadingWorldHistory = false;
                Debug.LogWarning($"[ChatUIManager] Load world chat history failed: {error}");
            });
    }

    // Executes core business logic for add world message.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for add message.
    public void AddMessage(string sender, string message, bool isMe)
    {
        Color senderColor = isMe ? myNameColor : otherNameColor;
        AddMessage(sender, message, senderColor, 0, 0, isMe, false);
    }

    // Executes core business logic for prepare runtime bindings.
    private void PrepareRuntimeBindings()
    {
        AutoFindChannelTabs();
        BindSendEvents();
        BindChannelTabs();
        BindPartyStaticEvent();
    }

    // Executes core business logic for bind send events.
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
            inputField.characterLimit = NetworkChatText.MaxContentChars;
            inputField.onSubmit.AddListener(HandleInputSubmitted);
        }
    }

    // Executes core business logic for handle input submitted.
    // Logic details: validates required non-empty string arguments.
    private void HandleInputSubmitted(string _)
    {
        OnSendClicked();
    }

    // Executes core business logic for auto find channel tabs.
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

    // Executes core business logic for get button label.
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

    // Executes core business logic for bind channel tabs.
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

    // Executes core business logic for bind party static event.
    private void BindPartyStaticEvent()
    {
        if (partyStaticEventBound)
        {
            return;
        }

        partyStaticEventBound = true;
        PartyLobby.OnLocalPartyChanged += HandleLocalPartyChanged;
    }

    // Executes core business logic for unbind party static event.
    private void UnbindPartyStaticEvent()
    {
        if (!partyStaticEventBound)
        {
            return;
        }

        partyStaticEventBound = false;
        PartyLobby.OnLocalPartyChanged -= HandleLocalPartyChanged;
    }

    // Executes core business logic for switch channel.
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

    // Executes core business logic for load current channel history.
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
                SubscribePartyTransport();
                if (HasNoParty())
                {
                    AddSystemMessage("You are not in a party.");
                }
                FlushPendingPartyMessages();
                break;
        }
    }

    // Executes core business logic for subscribe channel relays.
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
            SubscribePartyTransport();
        }
        else
        {
            UnsubscribePartyTransport();
        }
    }

    // Executes core business logic for send world message.
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

                PlayerPresence.BroadcastWorldMessage(message);

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

    // Executes core business logic for send guild message.
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

    // Executes core business logic for send party message.
    private void SendPartyMessage(string msg)
    {
        bool useDungeonTransport = IsDungeonPartyChat();

        var party = PartyLobby.Local;
        if (!useDungeonTransport && party == null)
        {
            AddSystemMessage(IsPartyTransportMigrating()
                ? "Entering the dungeon — party chat will be back in a moment."
                : "You are not in a party.");
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

        bool sent = useDungeonTransport
            ? NetworkPlayer.BroadcastPartyChat(message)
            : party.BroadcastPartyMessage(message);

        if (!sent)
        {
            inputField.text = msg;
            AddSystemMessage("Party chat is not ready.");
        }
        else
        {
            AddPartyMessage(message);
        }

        FocusInput();
    }

    // Executes core business logic for load guild history with resolved guild.
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

    // Executes core business logic for load guild history.
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
                Debug.LogWarning($"[ChatUIManager] Load guild chat failed: {BuildErrorMessage(error)}");
            });
    }

    // Executes core business logic for resolve guild id.
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

    // Executes core business logic for add guild message.
    // Logic details: validates required non-empty string arguments.
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
        AddMessage(sender, message.content, senderColor, message.messageId, message.senderId, isMe, false);
    }

    // Executes core business logic for add party message.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for add system message.
    private void AddSystemMessage(string message)
    {
        AddMessage("System", message, systemNameColor);
    }

    // Executes core business logic for add message.
    private void AddMessage(string sender, string message, Color senderColor, int chatMessageId = 0, int senderProfileId = 0, bool isMine = true, bool isReported = false)
    {
        if (chatMessagePrefab == null || contentParent == null)
        {
            Debug.LogError($"[ChatUIManager] AddMessage SKIP: chatMessagePrefab={chatMessagePrefab} contentParent={contentParent}");
            return;
        }

        UIChatMessage newMsg = Instantiate(chatMessagePrefab, contentParent);
        newMsg.gameObject.SetActive(true);
        newMsg.Setup(sender, message, senderColor, new Color(0, 0, 0, 0), chatMessageId, senderProfileId, isMine, isReported);

        newMsg.OnSenderClicked += HandleSenderNameClicked;
        newMsg.OnReportClicked += HandleWorldReportClicked;

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(ScrollToBottom());
    }

    // Executes core business logic for clear messages.
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

    // Executes core business logic for handle sender name clicked.
    // Logic details: validates numeric boundary constraints.
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
            Debug.LogError("[ChatUIManager] Player context menu is not assigned in Inspector.");
        }
    }

    // Executes core business logic for handle world report clicked.
    private void HandleWorldReportClicked(UIChatMessage item)
    {
        if (item == null || (item.ChatMessageId > 0 && pendingReportIds.Contains(item.ChatMessageId)))
        {
            return;
        }

        if (reportConfirmPopup == null)
        {
            reportConfirmPopup = FindFirstObjectByType<UIReportConfirmPopup>(FindObjectsInactive.Include);
        }

        string targetDescription = item != null && !string.IsNullOrWhiteSpace(item.SenderProfileId > 0 ? $"Player {item.SenderProfileId}" : null)
            ? $"Player {item.SenderProfileId}"
            : "this message";

        if (reportConfirmPopup != null)
        {
            reportConfirmPopup.ShowPopup(targetDescription, () => ExecuteReport(item));
        }
        else
        {
            ExecuteReport(item);
        }
    }

    // Executes core business logic for execute report.
    // Logic details: validates numeric boundary constraints.
    private void ExecuteReport(UIChatMessage item)
    {
        if (item.ChatMessageId <= 0)
        {
            item.MarkReported();
            AddSystemMessage("Report submitted.");
            return;
        }

        pendingReportIds.Add(item.ChatMessageId);
        ChatApi.Instance.ReportWorldMessage(
            item.ChatMessageId,
            "Reported from chat UI",
            response =>
            {
                pendingReportIds.Remove(item.ChatMessageId);
                item.MarkReported();
                Debug.Log($"[ChatUIManager] ReportWorldMessage submitted. ChatMessageId={item.ChatMessageId}");
            },
            error =>
            {
                pendingReportIds.Remove(item.ChatMessageId);
                Debug.LogWarning($"[ChatUIManager] ReportWorldMessage failed: {BuildErrorMessage(error)}");
            });
    }

    // Executes core business logic for populate world history.
    private void PopulateWorldHistory(PagedResultResponse<WorldChatMessageResponse> response)
    {
        if (response == null || response.Items == null)
        {
            Debug.LogWarning("[ChatUIManager] PopulateWorldHistory: response or Items is null.");
            return;
        }

        Debug.Log($"[ChatUIManager] PopulateWorldHistory: {response.Items.Length} messages");
        foreach (var message in response.Items)
        {
            AddWorldMessage(message);
        }
    }

    // Executes core business logic for subscribe photon relay.
    private void SubscribePhotonRelay()
    {
        if (worldRelayBound)
        {
            return;
        }

        PlayerPresence.OnWorldMessageReceived += OnPhotonWorldMessageReceived;
        worldRelayBound = true;
    }

    // Executes core business logic for unsubscribe photon relay.
    private void UnsubscribePhotonRelay()
    {
        if (!worldRelayBound)
        {
            return;
        }

        PlayerPresence.OnWorldMessageReceived -= OnPhotonWorldMessageReceived;
        worldRelayBound = false;
    }


    // Executes core business logic for is dungeon party chat.
    // Returns a boolean indicating operation success.
    private static bool IsDungeonPartyChat()
    {
        var photon = PhotonManager.Instance;
        return photon != null && photon.IsDungeonSession && NetworkPlayer.CanUsePartyChat;
    }

    // Executes core business logic for is party transport migrating.
    // Returns a boolean indicating operation success.
    private static bool IsPartyTransportMigrating()
    {
        return PartyManager.IsEnteringDungeon;
    }

    // Executes core business logic for has no party.
    // Returns a boolean indicating operation success.
    private static bool HasNoParty()
    {
        return PartyLobby.Local == null && !IsDungeonPartyChat() && !IsPartyTransportMigrating();
    }

    // Executes core business logic for subscribe party transport.
    private void SubscribePartyTransport()
    {
        BindPartyNetworkEvent();
        SubscribePartyLobby();
    }

    // Executes core business logic for unsubscribe party transport.
    private void UnsubscribePartyTransport()
    {
        UnsubscribePartyLobby();
        UnbindPartyNetworkEvent();
    }

    // Executes core business logic for bind party network event.
    private void BindPartyNetworkEvent()
    {
        if (partyNetworkEventBound)
        {
            return;
        }

        NetworkPlayer.PartyChatReceived += OnPartyMessageReceived;
        partyNetworkEventBound = true;
    }

    // Executes core business logic for unbind party network event.
    private void UnbindPartyNetworkEvent()
    {
        if (!partyNetworkEventBound)
        {
            return;
        }

        NetworkPlayer.PartyChatReceived -= OnPartyMessageReceived;
        partyNetworkEventBound = false;
    }

    // Executes core business logic for subscribe party lobby.
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

    // Executes core business logic for unsubscribe party lobby.
    private void UnsubscribePartyLobby()
    {
        if (subscribedParty == null)
        {
            return;
        }

        subscribedParty.PartyMessageReceived -= OnPartyMessageReceived;
        subscribedParty = null;
    }

    // Executes core business logic for handle local party changed.
    private void HandleLocalPartyChanged()
    {
        if (currentChannel != ChatChannel.Party)
        {
            return;
        }

        var previous = subscribedParty;
        SubscribePartyTransport();

        if (previous != null && subscribedParty == null && !HasNoParty())
        {
            return;
        }

        if (clearPartyChatOnPartyChanged && previous != subscribedParty)
        {
            ClearMessages();
            if (HasNoParty())
            {
                AddSystemMessage("You are not in a party.");
            }
        }
    }

    // Executes core business logic for update history fallback state.
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

    // Executes core business logic for has ready photon relay.
    // Returns a boolean indicating operation success.
    private static bool HasReadyPhotonRelay()
    {
        return PlayerPresence.WorldChatReady;
    }

    // Executes core business logic for start history fallback.
    private void StartHistoryFallback()
    {
        if (fallbackHistoryCoroutine != null)
        {
            return;
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        fallbackHistoryCoroutine = StartCoroutine(RefreshHistoryWithoutPhoton());
    }

    // Executes core business logic for stop history fallback.
    private void StopHistoryFallback()
    {
        if (fallbackHistoryCoroutine == null)
        {
            return;
        }

        StopCoroutine(fallbackHistoryCoroutine);
        fallbackHistoryCoroutine = null;
    }

    // Executes core business logic for refresh history without photon.
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

    // Executes core business logic for start guild refresh.
    private void StartGuildRefresh()
    {
        if (guildRefreshCoroutine != null)
        {
            return;
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        guildRefreshCoroutine = StartCoroutine(RefreshGuildChat());
    }

    // Executes core business logic for stop guild refresh.
    private void StopGuildRefresh()
    {
        if (guildRefreshCoroutine == null)
        {
            return;
        }

        StopCoroutine(guildRefreshCoroutine);
        guildRefreshCoroutine = null;
    }

    // Executes core business logic for refresh guild chat.
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

    // Executes core business logic for on photon world message received.
    private void OnPhotonWorldMessageReceived(WorldChatMessageResponse message)
    {
        if (currentChannel == ChatChannel.World)
        {
            AddWorldMessage(message);
        }
    }

    // Executes core business logic for on party message received.
    private void OnPartyMessageReceived(PartyChatMessageResponse message)
    {
        if (currentChannel == ChatChannel.Party)
        {
            AddPartyMessage(message);
            return;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.Content)) return;

        if (pendingPartyMessages.Count >= MaxPendingPartyMessages)
        {
            pendingPartyMessages.Dequeue();
        }

        pendingPartyMessages.Enqueue(message);
    }

    // Executes core business logic for flush pending party messages.
    private void FlushPendingPartyMessages()
    {
        while (pendingPartyMessages.Count > 0)
        {
            AddPartyMessage(pendingPartyMessages.Dequeue());
        }
    }

    // Executes core business logic for is current player.
    // Logic details: validates numeric boundary constraints.
    // Returns a boolean indicating operation success.
    private static bool IsCurrentPlayer(int senderId)
    {
        int currentPlayerId = GetCurrentPlayerProfileId();
        return currentPlayerId > 0 && senderId == currentPlayerId;
    }

    // Executes core business logic for get current player profile id.
    // Logic details: validates numeric boundary constraints.
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

    // Executes core business logic for get current player name.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for resolve sender name.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for build error message.
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

    // Executes core business logic for set sending.
    private void SetSending(bool sending)
    {
        isSending = sending;
        RefreshSendButtonState();
    }

    // Executes core business logic for start send cooldown.
    private void StartSendCooldown()
    {
        if (sendCooldownCoroutine != null)
        {
            StopCoroutine(sendCooldownCoroutine);
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        sendCooldownCoroutine = StartCoroutine(SendCooldownRoutine());
    }

    // Executes core business logic for cancel send cooldown.
    private void CancelSendCooldown()
    {
        if (sendCooldownCoroutine != null)
        {
            StopCoroutine(sendCooldownCoroutine);
            sendCooldownCoroutine = null;
        }

        isOnCooldown = false;
        RestoreSendButtonLabel();
        RefreshSendButtonState();
    }

    // Executes core business logic for send cooldown routine.
    // Logic details: validates required non-empty string arguments.
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

            yield return new WaitForSecondsRealtime(1f);
            remaining -= 1f;
        }

        isOnCooldown = false;
        sendCooldownCoroutine = null;
        RestoreSendButtonLabel();
        RefreshSendButtonState();
    }

    // Executes core business logic for get send button label.
    // Logic details: validates required non-empty string arguments.
    private TMP_Text GetSendButtonLabel()
    {
        return sendButton != null ? sendButton.GetComponentInChildren<TMP_Text>() : null;
    }

    // Executes core business logic for restore send button label.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for refresh send button state.
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

    // Executes core business logic for scroll to bottom.
    private IEnumerator ScrollToBottom()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // Executes core business logic for focus input.
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
