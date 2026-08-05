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

    // Party messages that arrived while another tab was open, replayed when the Party
    // tab opens. World and Guild do NOT need this — both are persisted server-side and
    // re-fetched on tab switch, so dropping a live copy only costs a few seconds of
    // latency. ChatApi has no party endpoint at all (only World and Friend), so the RPC
    // is the one and only copy: dropping it here loses the message permanently.
    // ponytail: capped in-memory ring, cleared on scene reload. If party history needs to
    // survive a relog, it needs a BE endpoint + LoadPartyHistory, not a bigger buffer.
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
        // Re-poll because both party transports appear asynchronously: PartyLobby is
        // spawned by the host, and the dungeon one only exists once the migration lands.
        // Checked independently — a single combined condition could leave the network
        // event unbound forever once both PartyLobby references settled on null.
        if (currentChannel == ChatChannel.Party &&
            (!partyNetworkEventBound || subscribedParty != PartyLobby.Local))
        {
            SubscribePartyTransport();
        }

        UpdateHistoryFallbackState();
    }

    private void OnDisable()
    {
        StopHistoryFallback();
        StopGuildRefresh();
        UnsubscribePhotonRelay();
        UnsubscribePartyTransport();
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
            // Stop typing at the wire budget instead of letting ClampUtf8 cut the message
            // silently — otherwise the sender sees their full text and everyone else sees it
            // truncated. Set in code, not the Inspector, so it can't drift from the RPC limit.
            inputField.characterLimit = NetworkChatText.MaxContentChars;
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
                SubscribePartyTransport();
                if (HasNoParty())
                {
                    AddSystemMessage("You are not in a party.");
                }
                FlushPendingPartyMessages();
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
            SubscribePartyTransport();
        }
        else
        {
            UnsubscribePartyTransport();
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
            // Echo our own message locally instead of waiting for the RpcTargets.All
            // round-trip to come back. Previously the sender saw nothing at all if the
            // echo was dropped or the receive handler was not bound yet — and the input
            // field had already been cleared, so the text was gone too. AddPartyMessage
            // dedups on SenderId|SentAt|Content, so the incoming echo is a no-op.
            AddPartyMessage(message);
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
        AddMessage(sender, message.content, senderColor, message.messageId, message.senderId, isMe, false);
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
        if (item == null || (item.ChatMessageId > 0 && pendingReportIds.Contains(item.ChatMessageId)))
        {
            return;
        }

        if (reportConfirmPopup == null)
        {
            reportConfirmPopup = FindObjectOfType<UIReportConfirmPopup>(true);
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

    // The relay event is static (it fires for messages arriving on ANY player's presence),
    // so subscribing does not depend on Photon being connected yet — no polling needed.
    private void SubscribePhotonRelay()
    {
        if (worldRelayBound)
        {
            return;
        }

        PlayerPresence.OnWorldMessageReceived += OnPhotonWorldMessageReceived;
        worldRelayBound = true;
    }

    private void UnsubscribePhotonRelay()
    {
        if (!worldRelayBound)
        {
            return;
        }

        PlayerPresence.OnWorldMessageReceived -= OnPhotonWorldMessageReceived;
        worldRelayBound = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Party transport
    //
    // Party chat has TWO transports because the party outlives the room it was
    // formed in:
    //   • Social lobby → PartyLobby (a NetworkObject of MYSTIC_SOCIAL_LOBBY).
    //   • Dungeon      → NetworkPlayer, because entering a dungeon tears the runner
    //                    down (PhotonManager.MigrateToDungeonAsync) and despawns
    //                    PartyLobby, nulling PartyLobby.Local for the whole run.
    // The dungeon room is capped at PartyLobby.MaxMembers and holds exactly one
    // party, so "everyone in the room" == "the party" there.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True while party chat should run over the dungeon room instead of PartyLobby.</summary>
    private static bool IsDungeonPartyChat()
    {
        var photon = PhotonManager.Instance;
        return photon != null && photon.IsDungeonSession && NetworkPlayer.CanUsePartyChat;
    }

    /// <summary>
    /// True when there is no live transport but the player IS still in a party — the
    /// migration window where the lobby runner is already down and the dungeon room is
    /// not up yet. Telling the player they left their party here would be wrong.
    /// </summary>
    private static bool IsPartyTransportMigrating()
    {
        return PartyManager.IsEnteringDungeon;
    }

    /// <summary>True when the player genuinely has no party on either transport.</summary>
    private static bool HasNoParty()
    {
        return PartyLobby.Local == null && !IsDungeonPartyChat() && !IsPartyTransportMigrating();
    }

    private void SubscribePartyTransport()
    {
        // Bind BOTH transports whenever the Party tab is open, instead of picking one
        // based on IsDungeonPartyChat().
        //
        // Picking one was a receive-side trap: if IsDungeonPartyChat() was false at the
        // moment the tab opened (phase not yet Dungeon, or the local avatar not spawned
        // yet), this bound PartyLobby instead — and in a dungeon PartyLobby.Local is null
        // forever, so subscribedParty and PartyLobby.Local were BOTH null and Update()'s
        // re-poll condition `subscribedParty != PartyLobby.Local` stayed false. The network
        // event then never got bound at all: that client could send (the send path re-checks
        // live) but could never receive. Only the sender's own local echo showed up.
        //
        // Binding both is safe: the two transports never carry the same message (the lobby
        // one is despawned in a dungeon), and AddPartyMessage dedups on
        // SenderId|SentAt|Content anyway.
        BindPartyNetworkEvent();
        SubscribePartyLobby();
    }

    private void UnsubscribePartyTransport()
    {
        UnsubscribePartyLobby();
        UnbindPartyNetworkEvent();
    }

    private void BindPartyNetworkEvent()
    {
        if (partyNetworkEventBound)
        {
            return;
        }

        NetworkPlayer.PartyChatReceived += OnPartyMessageReceived;
        partyNetworkEventBound = true;
    }

    private void UnbindPartyNetworkEvent()
    {
        if (!partyNetworkEventBound)
        {
            return;
        }

        NetworkPlayer.PartyChatReceived -= OnPartyMessageReceived;
        partyNetworkEventBound = false;
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
        SubscribePartyTransport();

        // PartyLobby despawning is the NORMAL start of dungeon entry, not a party
        // breakup — keep the history and stay quiet while the transport swaps over.
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

    private static bool HasReadyPhotonRelay()
    {
        return PlayerPresence.WorldChatReady;
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
            return;
        }

        // Not on the Party tab: hold it instead of dropping it. There is no party history
        // endpoint to re-fetch from, so a discard here is permanent data loss.
        if (message == null || string.IsNullOrWhiteSpace(message.Content)) return;

        if (pendingPartyMessages.Count >= MaxPendingPartyMessages)
        {
            pendingPartyMessages.Dequeue();
        }

        pendingPartyMessages.Enqueue(message);
    }

    /// <summary>
    /// Replay party messages that arrived while another tab was open. Called on entering
    /// the Party tab, AFTER ClearMessages() so the replay is not wiped by the same switch.
    /// AddPartyMessage dedups on SenderId|SentAt|Content, so a message that also arrived
    /// live is not doubled.
    /// </summary>
    private void FlushPendingPartyMessages()
    {
        while (pendingPartyMessages.Count > 0)
        {
            AddPartyMessage(pendingPartyMessages.Dequeue());
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
