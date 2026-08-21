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
using UnityEngine.EventSystems;
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
    private readonly HashSet<UIChatMessage> pendingRealtimeReports = new HashSet<UIChatMessage>();
    private readonly HashSet<string> displayedRealtimeKeys = new HashSet<string>();

    private const int MaxPendingPartyMessages = 50;
    private const int MaxPartyHistoryMessages = 100;
    private readonly Queue<PartyChatMessageResponse> pendingPartyMessages = new Queue<PartyChatMessageResponse>();
    private readonly List<PartyChatMessageResponse> partyMessageHistory = new List<PartyChatMessageResponse>();
    private readonly HashSet<string> cachedPartyMessageKeys = new HashSet<string>();
    private int partyHistorySessionKey;
    private bool hasPartyHistorySession;

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
    private Color sendButtonOriginalColor = Color.white;
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
        bool historyClearedOnOpen = SynchronizePartyHistorySession();
        if (historyClearedOnOpen && currentChannel == ChatChannel.Party)
            ClearMessages();
        SubscribeChannelRelays(); // Bind Photon realtime RPC events and party relays

        if (ApiClient.Instance.HasToken())
        {
            GuildApi.GetMyGuild(
                detail => { currentGuildId = detail != null ? detail.guildId : 0; RefreshChannelTabsVisibility(); },
                _ => { currentGuildId = 0; RefreshChannelTabsVisibility(); });
        }

        RefreshChannelTabsVisibility();

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
        RefreshChannelTabsVisibility();
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

    private readonly List<RaycastResult> tempRaycastResults = new List<RaycastResult>();

    // Dismisses chat modal if the user clicks outside its UI bounds.
    private void CheckClickOutside()
    {
        if (Time.unscaledTime - enableTime < 0.15f)
            return; // Debounce immediate click on open frame

        if (Input.GetMouseButtonDown(0))
        {
            var activePopup = UIPopupBox.FindPopup(transform);
            if (activePopup != null && activePopup.gameObject.activeInHierarchy)
            {
                return; // An alert or confirmation popup box is open; DO NOT close chat panel!
            }

            if (EventSystem.current != null)
            {
                var eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                tempRaycastResults.Clear();
                EventSystem.current.RaycastAll(eventData, tempRaycastResults);
                foreach (var result in tempRaycastResults)
                {
                    if (result.gameObject != null)
                    {
                        Transform t = result.gameObject.transform;
                        if (t.IsChildOf(transform) ||
                            (contextMenu != null && t.IsChildOf(contextMenu.transform)) ||
                            (reportConfirmPopup != null && t.IsChildOf(reportConfirmPopup.transform)) ||
                            (activePopup != null && t.IsChildOf(activePopup)) ||
                            t.name.Contains("Popup") || t.name.Contains("Modal") || t.name.Contains("Dialog"))
                        {
                            return; // Raycast hit chat UI or active popup element, do NOT close!
                        }
                    }
                }
            }

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
            if (!UIPopupBox.Notify(transform, "Notice", "Please enter a message before sending.", FocusInput))
            {
                AddSystemMessage("Please enter a message before sending.");
                FocusInput();
            }
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
        AddMessage(sender, message.Content, senderColor, message.ChatMessageId, message.SenderId, isMe, message.IsReported, "World");
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
        if (sendButton != null)
        {
            foreach (var tmp in sendButton.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.raycastTarget = false;
            }
            foreach (var txt in sendButton.GetComponentsInChildren<Text>(true))
            {
                txt.raycastTarget = false;
            }

            var graphic = sendButton.targetGraphic as Graphic;
            if (graphic == null)
            {
                graphic = sendButton.GetComponent<Graphic>();
                if (graphic != null) sendButton.targetGraphic = graphic;
            }
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }

            if (sendButton.GetComponent<UIHoverScaleEffect>() == null)
            {
                sendButton.gameObject.AddComponent<UIHoverScaleEffect>();
            }

            if (!sendEventsBound)
            {
                sendButton.onClick.AddListener(OnSendClicked);
            }
        }

        if (inputField != null)
        {
            inputField.characterLimit = NetworkChatText.MaxContentChars;

            var inputGraphic = inputField.targetGraphic as Graphic;
            if (inputGraphic == null)
            {
                inputGraphic = inputField.GetComponent<Graphic>();
                if (inputGraphic != null) inputField.targetGraphic = inputGraphic;
            }
            if (inputGraphic != null)
            {
                inputGraphic.raycastTarget = true;
            }

            if (inputField.textComponent != null)
            {
                inputField.textComponent.raycastTarget = false;
            }

            if (inputField.placeholder != null && inputField.placeholder is Graphic placeholderGraphic)
            {
                placeholderGraphic.raycastTarget = false;
            }

            if (!sendEventsBound)
            {
                inputField.onSubmit.AddListener(HandleInputSubmitted);
            }
        }

        sendEventsBound = true;
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

    // Configures tab button raycasts and hover effects for smooth responsiveness.
    private static void ConfigureTabButton(Button button)
    {
        if (button == null) return;

        foreach (var tmp in button.GetComponentsInChildren<TMP_Text>(true))
        {
            tmp.raycastTarget = false;
        }

        foreach (var text in button.GetComponentsInChildren<Text>(true))
        {
            text.raycastTarget = false;
        }

        var graphic = button.targetGraphic as Graphic;
        if (graphic == null)
        {
            graphic = button.GetComponent<Graphic>();
            if (graphic != null) button.targetGraphic = graphic;
        }
        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }

        if (button.GetComponent<UIHoverScaleEffect>() == null)
        {
            button.gameObject.AddComponent<UIHoverScaleEffect>();
        }
    }

    // Executes core business logic for bind channel tabs.
    private void BindChannelTabs()
    {
        if (worldTabButton != null)
        {
            ConfigureTabButton(worldTabButton);
            if (!worldTabBound)
            {
                worldTabBound = true;
                worldTabButton.onClick.AddListener(ShowWorldChat);
            }
        }

        if (guildTabButton != null)
        {
            ConfigureTabButton(guildTabButton);
            if (!guildTabBound)
            {
                guildTabBound = true;
                guildTabButton.onClick.AddListener(ShowGuildChat);
            }
        }

        if (partyTabButton != null)
        {
            ConfigureTabButton(partyTabButton);
            if (!partyTabBound)
            {
                partyTabBound = true;
                partyTabButton.onClick.AddListener(ShowPartyChat);
            }
        }
        RefreshChannelTabsVisibility();
    }

    private float cachedTabWidth = -1f;

    // Updates visibility of Guild and Party tabs based on player participation.
    public void RefreshChannelTabsVisibility()
    {
        if (worldTabButton != null)
        {
            worldTabButton.gameObject.SetActive(true);
        }

        bool hasGuild = currentGuildId > 0;
        if (guildTabButton != null)
        {
            guildTabButton.gameObject.SetActive(hasGuild);
        }

        bool hasParty = PartyLobby.Local != null || IsDungeonPartyChat() || IsPartyTransportMigrating();
        if (partyTabButton != null)
        {
            partyTabButton.gameObject.SetActive(hasParty);
        }

        LockTabButtonSizes();

        if ((currentChannel == ChatChannel.Guild && !hasGuild) ||
            (currentChannel == ChatChannel.Party && !hasParty))
        {
            ShowWorldChat();
        }
    }

    // Locks tab button widths to their original 3-tab proportions to prevent stretching when tabs hide.
    private void LockTabButtonSizes()
    {
        if (worldTabButton == null) return;

        Transform parentTransform = worldTabButton.transform.parent;
        if (parentTransform != null)
        {
            foreach (var hlg in parentTransform.GetComponents<HorizontalLayoutGroup>())
            {
                hlg.childForceExpandWidth = false;
                hlg.childAlignment = TextAnchor.UpperLeft;
            }
        }

        if (cachedTabWidth <= 0f)
        {
            var rt = worldTabButton.GetComponent<RectTransform>();
            if (rt != null && rt.rect.width > 0f)
            {
                cachedTabWidth = rt.rect.width;
            }
            else if (rt != null && rt.sizeDelta.x > 0f)
            {
                cachedTabWidth = rt.sizeDelta.x;
            }
            else if (parentTransform is RectTransform parentRt && parentRt.rect.width > 0f)
            {
                cachedTabWidth = (parentRt.rect.width - 12f) / 3f;
            }
        }

        if (cachedTabWidth > 0f)
        {
            ApplyPreferredWidth(worldTabButton, cachedTabWidth);
            ApplyPreferredWidth(guildTabButton, cachedTabWidth);
            ApplyPreferredWidth(partyTabButton, cachedTabWidth);
        }
    }

    private static void ApplyPreferredWidth(Button button, float width)
    {
        if (button == null) return;
        var le = button.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = button.gameObject.AddComponent<LayoutElement>();
        }
        le.preferredWidth = width;
        le.flexibleWidth = 0f;

        var rt = button.GetComponent<RectTransform>();
        if (rt != null)
        {
            var size = rt.sizeDelta;
            size.x = width;
            rt.sizeDelta = size;
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
                if (SynchronizePartyHistorySession())
                    ClearMessages();
                RenderPartyHistory();
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
            RefreshChannelTabsVisibility();
            onResolved?.Invoke(currentGuildId);
            return;
        }

        GuildApi.GetMyGuild(
            detail =>
            {
                currentGuildId = detail != null ? detail.guildId : 0;
                RefreshChannelTabsVisibility();

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
                RefreshChannelTabsVisibility();
                onFailed?.Invoke(BuildErrorMessage(error));
            });
    }

    private static int GetPartySessionKey(PartyLobby party)
    {
        return ReferenceEquals(party, null) ? 0 : party.GetInstanceID();
    }

    // Returns true when stale history was cleared because the party session changed or ended.
    private bool SynchronizePartyHistorySession()
    {
        int currentSessionKey = GetPartySessionKey(PartyLobby.Local);
        bool transportMigrating = IsPartyTransportMigrating() || IsDungeonPartyChat();

        if (currentSessionKey != 0)
        {
            bool changedSession = hasPartyHistorySession &&
                                  partyHistorySessionKey != currentSessionKey;
            bool cleared = clearPartyChatOnPartyChanged && changedSession;
            if (cleared)
                ClearPartyHistory();

            partyHistorySessionKey = currentSessionKey;
            hasPartyHistorySession = true;
            return cleared;
        }

        bool endedSession = hasPartyHistorySession && !transportMigrating;
        bool clearedEndedSession = clearPartyChatOnPartyChanged && endedSession;
        if (clearedEndedSession)
            ClearPartyHistory();

        return clearedEndedSession;
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

    private static string GetPartyMessageKey(PartyChatMessageResponse message)
    {
        if (message == null)
            return string.Empty;

        return $"{message.SenderId}|{message.SentAt}|{message.Content}";
    }

    private void CachePartyMessage(PartyChatMessageResponse message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content))
            return;

        SynchronizePartyHistorySession();

        string key = GetPartyMessageKey(message);
        if (string.IsNullOrEmpty(key) || !cachedPartyMessageKeys.Add(key))
            return;

        partyMessageHistory.Add(message);
        if (partyMessageHistory.Count > MaxPartyHistoryMessages)
        {
            var removed = partyMessageHistory[0];
            partyMessageHistory.RemoveAt(0);
            cachedPartyMessageKeys.Remove(GetPartyMessageKey(removed));
        }
    }

    private void RenderPartyHistory()
    {
        foreach (var message in partyMessageHistory)
            AddPartyMessage(message);
    }

    private void ClearPartyHistory()
    {
        partyMessageHistory.Clear();
        cachedPartyMessageKeys.Clear();
        pendingPartyMessages.Clear();
        partyHistorySessionKey = 0;
        hasPartyHistorySession = false;
    }

    // Executes core business logic for add party message.
    // Logic details: validates required non-empty string arguments.
    private void AddPartyMessage(PartyChatMessageResponse message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content))
        {
            return;
        }

        CachePartyMessage(message);
        string key = GetPartyMessageKey(message);
        if (!displayedRealtimeKeys.Add(key))
        {
            return;
        }

        bool isMe = IsCurrentPlayer(message.SenderId);
        string sender = ResolveSenderName(message.SenderId, message.SenderName, isMe);
        Color senderColor = isMe ? myNameColor : otherNameColor;
        AddMessage(sender, message.Content, senderColor, 0, message.SenderId, isMe, false, "Party");
    }

    // Executes core business logic for add system message.
    private void AddSystemMessage(string message)
    {
        AddMessage("System", message, systemNameColor);
    }

    // Executes core business logic for add message.
    private void AddMessage(string sender, string message, Color senderColor, int chatMessageId = 0, int senderProfileId = 0, bool isMine = true, bool isReported = false, string reportChannel = null)
    {
        if (chatMessagePrefab == null || contentParent == null)
        {
            Debug.LogError($"[ChatUIManager] AddMessage SKIP: chatMessagePrefab={chatMessagePrefab} contentParent={contentParent}");
            return;
        }

        UIChatMessage newMsg = Instantiate(chatMessagePrefab, contentParent);
        newMsg.gameObject.SetActive(true);
        newMsg.Setup(sender, message, senderColor, new Color(0, 0, 0, 0), chatMessageId, senderProfileId, isMine, isReported,
            string.IsNullOrWhiteSpace(reportChannel) ? currentChannel.ToString() : reportChannel);

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
        pendingRealtimeReports.Clear();
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
        string myName = GetCurrentPlayerName();
        if (!string.IsNullOrEmpty(senderName) && !string.IsNullOrEmpty(myName) &&
            string.Equals(senderName.Trim(), myName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return; // Don't open context menu for self!
        }

        if (IsCurrentPlayer(senderProfileId))
        {
            return; // Don't open context menu for self!
        }

        if (contextMenu == null)
        {
            contextMenu = FindFirstObjectByType<UIPlayerContextMenu>(FindObjectsInactive.Include);
        }

        if (contextMenu != null)
        {
            Vector3 pos = clickPosition != Vector3.zero ? clickPosition : Input.mousePosition;
            contextMenu.ShowMenu(senderName, senderProfileId, pos);
        }
        else
        {
            Debug.LogError("[ChatUIManager] Player context menu is not assigned in Inspector and could not be found.");
        }
    }

    // Executes core business logic for handle world report clicked.
    private void HandleWorldReportClicked(UIChatMessage item)
    {
        if (item == null ||
            (item.ChatMessageId > 0 && pendingReportIds.Contains(item.ChatMessageId)) ||
            (item.ChatMessageId <= 0 && pendingRealtimeReports.Contains(item)))
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
        if (item == null)
            return;

        bool isPartyMessage = string.Equals(item.Channel, "Party", StringComparison.OrdinalIgnoreCase);
        if (isPartyMessage)
        {
            if (item.SenderProfileId <= 0 || string.IsNullOrWhiteSpace(item.MessageContent))
            {
                AddSystemMessage("This party message cannot be reported.");
                return;
            }

            pendingRealtimeReports.Add(item);
            ChatApi.Instance.ReportPartyMessage(
                item.SenderProfileId,
                item.MessageContent,
                "Reported from party chat UI",
                response =>
                {
                    pendingRealtimeReports.Remove(item);
                    item.MarkReported();
                    AddSystemMessage("Report submitted. The review result will be sent to your mailbox.");
                    MysticJourney.Screen.Mail.MailboxUIManager.NotifyMailboxChanged();
                    Debug.Log($"[ChatUIManager] ReportPartyMessage submitted. SenderProfileId={item.SenderProfileId}");
                },
                error =>
                {
                    pendingRealtimeReports.Remove(item);
                    Debug.LogWarning($"[ChatUIManager] ReportPartyMessage failed: {BuildErrorMessage(error)}");
                });
            return;
        }

        if (item.ChatMessageId <= 0)
        {
            AddSystemMessage("This chat channel cannot be reported.");
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
                MysticJourney.Screen.Mail.MailboxUIManager.NotifyMailboxChanged();
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
        bool historyCleared = SynchronizePartyHistorySession();

        RefreshChannelTabsVisibility();
        if (currentChannel != ChatChannel.Party)
            return;

        if (historyCleared)
            ClearMessages();

        SubscribePartyTransport();
        if (HasNoParty())
            AddSystemMessage("You are not in a party.");
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

        CachePartyMessage(message);

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
        if (buttonLabel != null)
        {
            if (string.IsNullOrEmpty(sendButtonOriginalLabel))
            {
                sendButtonOriginalLabel = buttonLabel.text;
                sendButtonOriginalColor = buttonLabel.color;
            }
        }

        float remaining = sendCooldownSeconds;
        while (remaining > 0f)
        {
            if (currentChannel == ChatChannel.World)
            {
                if (sendButton != null)
                {
                    sendButton.interactable = false;
                }

                SetSendButtonIconVisible(false);

                if (buttonLabel != null)
                {
                    buttonLabel.gameObject.SetActive(true);
                    int sec = Mathf.CeilToInt(remaining);
                    buttonLabel.text = $"{sec}s";
                    buttonLabel.color = new Color(1f, 0.88f, 0.2f, 1f); // Vibrant gold/yellow text
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
        if (sendButton == null) return null;

        var tmp = sendButton.GetComponentInChildren<TMP_Text>(true);
        if (tmp == null)
        {
            var labelObject = new GameObject("CooldownText", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(sendButton.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            tmp = labelObject.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.88f, 0.2f, 1f);
            tmp.raycastTarget = false;
        }

        return tmp;
    }

    // Toggles child icon images on sendButton (e.g. speech bubble icon).
    private void SetSendButtonIconVisible(bool visible)
    {
        if (sendButton == null) return;
        foreach (var img in sendButton.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject != sendButton.gameObject && img != sendButton.targetGraphic)
            {
                img.enabled = visible;
            }
        }
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
                : string.Empty;
            buttonLabel.color = sendButtonOriginalColor != default ? sendButtonOriginalColor : Color.white;

            if (string.IsNullOrEmpty(sendButtonOriginalLabel))
            {
                buttonLabel.gameObject.SetActive(false);
            }
        }

        SetSendButtonIconVisible(true);
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
