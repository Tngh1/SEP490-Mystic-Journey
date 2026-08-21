using System.Collections;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using MysticJourney.Screen.Mail;
using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public sealed class HUDNotificationController : MonoBehaviour
{
    private const float MailRefreshIntervalSeconds = 20f;
    private const int MailLookupPageSize = 100;

    private GameObject _mailButtonObject;
    private GameObject _chatButtonObject;
    private Button _mailButton;
    private Button _chatButton;
    private GameObject _mailBadge;
    private GameObject _chatBadge;
    private ChatUIManager _chatPanel;
    private PartyLobby _subscribedParty;
    private Coroutine _mailRefreshCoroutine;
    private bool _configured;
    private bool _eventsBound;
    private bool _mailRequestInFlight;
    private bool _mailRefreshQueued;

    private static Sprite _circleSprite;

    // Executes configure operation.
    public void Configure(GameObject mailButtonObject, GameObject chatButtonObject)
    {
        if (_mailButtonObject == mailButtonObject &&
            _chatButtonObject == chatButtonObject &&
            _configured)
        {
            return;
        }

        UnbindButtonEvents();

        _mailButtonObject = mailButtonObject;
        _chatButtonObject = chatButtonObject;
        _mailButton = _mailButtonObject != null ? _mailButtonObject.GetComponent<Button>() : null;
        _chatButton = _chatButtonObject != null ? _chatButtonObject.GetComponent<Button>() : null;
        _mailBadge = EnsureBadge(_mailButtonObject, "MailNotificationBadge");
        _chatBadge = EnsureBadge(_chatButtonObject, "ChatNotificationBadge");
        _chatPanel = FindFirstObjectByType<ChatUIManager>(FindObjectsInactive.Include);
        _configured = true;

        BindButtonEvents();

        if (isActiveAndEnabled)
        {
            StartListening();
        }
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        if (_configured)
        {
            StartListening();
        }
    }

    // Per-frame update loop for HUDNotificationController.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (!_configured)
        {
            return;
        }

        if (_chatPanel == null)
        {
            _chatPanel = FindFirstObjectByType<ChatUIManager>(FindObjectsInactive.Include);
        }

        if (_subscribedParty != PartyLobby.Local)
        {
            SubscribeToLocalParty();
        }

        if (IsChatOpen())
        {
            SetBadgeVisible(_chatBadge, false);
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        StopListening();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        UnbindButtonEvents();
        StopListening();
    }

    // Executes start listening operation.
    private void StartListening()
    {
        if (!_eventsBound)
        {
            PlayerPresence.OnWorldMessageReceived += HandleWorldMessageReceived;
            NetworkPlayer.PartyChatReceived += HandlePartyMessageReceived;
            MailboxUIManager.MailboxStateChanged += RefreshMailboxStatus;
            _eventsBound = true;
        }

        SubscribeToLocalParty();

        if (_mailRefreshCoroutine == null)
        {
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            _mailRefreshCoroutine = StartCoroutine(RefreshMailboxLoop());
        }
    }

    // Executes stop listening operation.
    private void StopListening()
    {
        if (_eventsBound)
        {
            PlayerPresence.OnWorldMessageReceived -= HandleWorldMessageReceived;
            NetworkPlayer.PartyChatReceived -= HandlePartyMessageReceived;
            MailboxUIManager.MailboxStateChanged -= RefreshMailboxStatus;
            _eventsBound = false;
        }

        if (_subscribedParty != null)
        {
            _subscribedParty.PartyMessageReceived -= HandlePartyMessageReceived;
            _subscribedParty = null;
        }

        if (_mailRefreshCoroutine != null)
        {
            StopCoroutine(_mailRefreshCoroutine);
            _mailRefreshCoroutine = null;
        }
    }

    // Executes subscribe to local party operation.
    private void SubscribeToLocalParty()
    {
        if (_subscribedParty != null)
        {
            _subscribedParty.PartyMessageReceived -= HandlePartyMessageReceived;
        }

        _subscribedParty = PartyLobby.Local;
        if (_subscribedParty != null)
        {
            _subscribedParty.PartyMessageReceived += HandlePartyMessageReceived;
        }
    }

    // Update mailbox loop; it updates mailbox status.
    private IEnumerator RefreshMailboxLoop()
    {
        var wait = new WaitForSecondsRealtime(MailRefreshIntervalSeconds);

        while (isActiveAndEnabled)
        {
            RefreshMailboxStatus();
            yield return wait;
        }

        _mailRefreshCoroutine = null;
    }

    // Executes refresh mailbox status operation.
    private void RefreshMailboxStatus()
    {
        if (!Application.isPlaying)
            return;

        if (_mailRequestInFlight)
        {
            _mailRefreshQueued = true;
            return;
        }

        ApiClient apiClient = ApiClient.Instance;
        if (apiClient == null || !apiClient.HasToken())
        {
            _mailRefreshQueued = false;
            SetBadgeVisible(_mailBadge, false);
            return;
        }

        _mailRequestInFlight = true;
        MailboxApi.Instance.GetMyMailboxes(
            1,
            MailLookupPageSize,
            response =>
            {
                if (this != null)
                {
                    bool hasUnread = response?.Items != null &&
                                     response.Items.Any(mail => mail != null && !mail.IsRead);
                    SetBadgeVisible(_mailBadge, hasUnread);
                }

                CompleteMailboxRefresh();
            },
            error =>
            {
                Debug.LogWarning($"[HUDNotificationController] Failed to refresh unread mail: {error.Message}");
                CompleteMailboxRefresh();
            });
    }

    private void CompleteMailboxRefresh()
    {
        _mailRequestInFlight = false;
        if (!_mailRefreshQueued || this == null)
            return;

        _mailRefreshQueued = false;
        RefreshMailboxStatus();
    }

    // Executes handle world message received operation.
    private void HandleWorldMessageReceived(WorldChatMessageResponse message)
    {
        if (message == null || IsCurrentPlayer(message.SenderId) || IsChatOpen())
        {
            return;
        }

        SetBadgeVisible(_chatBadge, true);
    }

    // Executes handle party message received operation.
    private void HandlePartyMessageReceived(PartyChatMessageResponse message)
    {
        if (message == null || IsCurrentPlayer(message.SenderId) || IsChatOpen())
        {
            return;
        }

        SetBadgeVisible(_chatBadge, true);
    }

    // Executes is chat open operation.
    private bool IsChatOpen()
    {
        return _chatPanel != null && _chatPanel.gameObject.activeInHierarchy;
    }

    // Executes is current player operation.
    private static bool IsCurrentPlayer(int senderId)
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

        return currentPlayerId > 0 && senderId == currentPlayerId;
    }

    // Executes bind button events operation.
    private void BindButtonEvents()
    {
        if (_mailButton != null)
        {
            _mailButton.onClick.AddListener(RefreshMailboxStatus);
        }

        if (_chatButton != null)
        {
            _chatButton.onClick.AddListener(ClearChatNotification);
        }
    }

    // Executes unbind button events operation.
    private void UnbindButtonEvents()
    {
        if (_mailButton != null)
        {
            _mailButton.onClick.RemoveListener(RefreshMailboxStatus);
        }

        if (_chatButton != null)
        {
            _chatButton.onClick.RemoveListener(ClearChatNotification);
        }
    }

    // Executes clear chat notification operation.
    private void ClearChatNotification()
    {
        SetBadgeVisible(_chatBadge, false);
    }

    // Executes ensure badge operation.
    private static GameObject EnsureBadge(GameObject buttonObject, string badgeName)
    {
        if (buttonObject == null)
        {
            return null;
        }

        Transform existing = buttonObject.transform.Find(badgeName);
        GameObject badge = existing != null ? existing.gameObject : new GameObject(badgeName, typeof(RectTransform));
        RectTransform rect = badge.GetComponent<RectTransform>();

        if (rect.parent != buttonObject.transform)
        {
            rect.SetParent(buttonObject.transform, false);
        }

        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-7f, -7f);
        rect.sizeDelta = new Vector2(16f, 16f);
        rect.localScale = Vector3.one;

        Image image = badge.GetComponent<Image>();
        if (image == null)
        {
            image = badge.AddComponent<Image>();
        }

        image.sprite = GetCircleSprite();
        image.color = new Color32(239, 59, 59, 255);
        image.raycastTarget = false;
        image.preserveAspect = true;

        Shadow shadow = badge.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = badge.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color32(82, 21, 21, 210);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;

        badge.transform.SetAsLastSibling();
        badge.SetActive(false);
        return badge;
    }

    // Executes get circle sprite operation.
    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null)
        {
            return _circleSprite;
        }

        const int size = 32;
        const float radius = 14.5f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "HUDNotificationDotTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 1f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        _circleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        _circleSprite.name = "HUDNotificationDotSprite";
        _circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return _circleSprite;
    }

    // Executes set badge visible operation.
    private static void SetBadgeVisible(GameObject badge, bool visible)
    {
        if (badge != null && badge.activeSelf != visible)
        {
            badge.SetActive(visible);
        }
    }
}
