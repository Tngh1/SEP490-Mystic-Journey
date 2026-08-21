using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class UIPlayerContextMenu : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text playerNameText;

    [Header("Buttons (keo vao Inspector hoac de auto-find)")]
    public Button viewProfileButton;
    public Button addFriendButton;
    public Button reportButton;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private static readonly HashSet<long> PendingOutgoingRequests = new HashSet<long>();
    private string currentPlayerName;
    private int currentPlayerProfileId;
    private float menuOpenTime = -1f;
    private int friendStatusRequestVersion;
    private const float ClickCooldown = 0.15f;

    private RectTransform menuRect;
    private Canvas parentCanvas;

    // Initializes internal component caches and dependencies for UIPlayerContextMenu upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        menuRect = transform as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        AutoFindButtons();
        BindButtons();
        EnsureButtonRaycasts();
        EnsureHoverEffects();
    }

    // Executes auto find buttons operation.
    private void AutoFindButtons()
    {
        foreach (var btn in GetComponentsInChildren<Button>(true))
        {
            var label = btn.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;

            string t = label.text.Trim().ToLowerInvariant();

            if (viewProfileButton == null && (t.Contains("profile") || t.Contains("view")))
            {
                viewProfileButton = btn;
            }
            else if (addFriendButton == null && (t.Contains("add") || t.Contains("friend")))
            {
                addFriendButton = btn;
            }
            else if (reportButton == null && t.Contains("report"))
            {
                reportButton = btn;
            }
        }

        Debug.Log($"[ContextMenu] AutoFind -> viewProfile={viewProfileButton != null}, addFriend={addFriendButton != null}, report={reportButton != null}");
    }

    // Executes bind buttons operation.
    private void BindButtons()
    {
        if (viewProfileButton != null)
        {
            viewProfileButton.onClick.RemoveListener(OnViewProfileClicked);
            viewProfileButton.onClick.AddListener(OnViewProfileClicked);
        }
        else
        {
            Debug.LogWarning("[ContextMenu] viewProfileButton not found!");
        }

        if (addFriendButton != null)
        {
            addFriendButton.onClick.RemoveListener(OnAddFriendClicked);
            addFriendButton.onClick.AddListener(OnAddFriendClicked);
        }
        else
        {
            Debug.LogWarning("[ContextMenu] addFriendButton not found!");
        }

        if (reportButton != null)
        {
            reportButton.onClick.RemoveListener(OnReportClicked);
            reportButton.onClick.AddListener(OnReportClicked);
        }
        else
        {
            Debug.LogWarning("[ContextMenu] reportButton not found!");
        }
    }

    // Per-frame update loop for UIPlayerContextMenu.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (Time.unscaledTime - menuOpenTime < ClickCooldown)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (IsPointerOverThisMenu())
            return;

        CloseMenu();
    }

    // Positions context menu modal next to clicked player avatar/message and checks friend status.
    public void ShowMenu(string playerName, int playerProfileId, Vector3 position)
    {
        currentPlayerName = playerName; // Cache target player username
        currentPlayerProfileId = playerProfileId; // Cache target profile ID
        menuOpenTime = Time.unscaledTime;

        if (menuRect == null) menuRect = transform as RectTransform;

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null && transform.parent != rootCanvas.transform)
        {
            transform.SetParent(rootCanvas.transform, false);
        }

        transform.SetAsLastSibling(); // Bring menu to front of root Canvas!
        gameObject.SetActive(true); // Open modal

        if (playerNameText != null)
            playerNameText.text = playerName; // Display target player name on header

        AutoFindButtons();
        BindButtons();
        EnsureButtonRaycasts();
        EnsureHoverEffects();

        bool isSelf = IsSelf(playerProfileId, playerName);

        if (reportButton != null)
        {
            reportButton.gameObject.SetActive(!isSelf);
        }

        if (isSelf)
        {
            HideAddFriendButton();
        }
        else if (HasCachedPendingRequest(currentPlayerProfileId))
        {
            SetAddFriendSent(); // Update button label to "Pending"
        }
        else
        {
            SetAddFriendChecking(); // Set loading spinner while querying friendship relation
            RefreshAddFriendVisibility(); // Query FriendApi to check if already friends
        }

        ReflowLayout();
        Vector3 targetPos = position != Vector3.zero ? position : Input.mousePosition;
        PositionMenuSmartly(targetPos);

        Debug.Log($"[ContextMenu] ShowMenu -> name={playerName} profileId={playerProfileId} pos={targetPos} isSelf={isSelf}");
    }

    // Helper to find the actual wooden card RectTransform panel inside the full-screen modal root.
    private RectTransform GetCardRect()
    {
        if (viewProfileButton != null)
        {
            Transform curr = viewProfileButton.transform;
            while (curr.parent != null && curr.parent != transform)
            {
                curr = curr.parent;
            }
            if (curr is RectTransform rt && curr != transform)
            {
                return rt;
            }
        }
        if (playerNameText != null)
        {
            Transform curr = playerNameText.transform;
            while (curr.parent != null && curr.parent != transform)
            {
                curr = curr.parent;
            }
            if (curr is RectTransform rt && curr != transform)
            {
                return rt;
            }
        }
        if (transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeSelf && child is RectTransform childRt)
                {
                    return childRt;
                }
            }
            if (transform.GetChild(0) is RectTransform firstRt) return firstRt;
        }
        return transform as RectTransform;
    }

    // Dynamic reflowing of menu layout containers so frame height wraps tightly around active buttons.
    private void ReflowLayout()
    {
        RectTransform cardRect = GetCardRect();
        if (cardRect == null) return;

        foreach (var lg in cardRect.GetComponentsInChildren<LayoutGroup>(true))
        {
            var rt = lg.GetComponent<RectTransform>();
            if (rt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    // Smart positioning that adjusts pivot (opening upwards/downwards) based on click location.
    private void PositionMenuSmartly(Vector3 clickPos)
    {
        RectTransform cardRect = GetCardRect();
        if (cardRect == null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector3 screenClick = RectTransformUtility.WorldToScreenPoint(cam, clickPos);

        // If click is in lower half of screen, expand UPWARDS (pivotY = 0), else DOWNWARDS (pivotY = 1)
        float pivotY = (screenClick.y < Screen.height * 0.55f) ? 0f : 1f;
        float pivotX = (screenClick.x > Screen.width * 0.75f) ? 1f : 0f;

        cardRect.pivot = new Vector2(pivotX, pivotY);

        var parentRt = cardRect.parent as RectTransform ?? cardRect;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRt, screenClick, cam, out Vector3 worldClick))
        {
            cardRect.position = worldClick;
        }
        else
        {
            cardRect.position = clickPos;
        }

        ReflowLayout();
        ClampToScreenBounds();
    }

    private void ClampToScreenBounds()
    {
        RectTransform cardRect = GetCardRect();
        if (cardRect == null) return;
        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        cardRect.GetWorldCorners(corners);

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector3 minScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector3 maxScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        float shiftScreenX = 0f;
        float shiftScreenY = 0f;
        float padding = 20f;

        if (minScreen.x < padding) shiftScreenX = padding - minScreen.x;
        else if (maxScreen.x > Screen.width - padding) shiftScreenX = (Screen.width - padding) - maxScreen.x;

        if (minScreen.y < padding) shiftScreenY = padding - minScreen.y;
        else if (maxScreen.y > Screen.height - padding) shiftScreenY = (Screen.height - padding) - maxScreen.y;

        if (Mathf.Abs(shiftScreenX) > 0.1f || Mathf.Abs(shiftScreenY) > 0.1f)
        {
            Vector3 currentScreenPos = RectTransformUtility.WorldToScreenPoint(cam, cardRect.position);
            Vector3 targetScreenPos = new Vector3(currentScreenPos.x + shiftScreenX, currentScreenPos.y + shiftScreenY, currentScreenPos.z);

            var parentRt = cardRect.parent as RectTransform ?? cardRect;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRt, targetScreenPos, cam, out Vector3 targetWorldPos))
            {
                cardRect.position = targetWorldPos;
            }
        }
    }

    // Dismisses player context menu popup.
    public void CloseMenu()
    {
        gameObject.SetActive(false); // Hide menu
    }

    // Opens player profile inspect card.
    private void OnViewProfileClicked()
    {
        Debug.Log($"[ContextMenu] ViewProfile clicked -> name={currentPlayerName} profileId={currentPlayerProfileId}");
        CloseMenu(); // Dismiss menu and route to profile inspector
    }

    // Sends outgoing friend request to target player profile.
    private void OnAddFriendClicked()
    {
        Debug.Log($"[ContextMenu] AddFriend clicked -> profileId={currentPlayerProfileId}");

        if (currentPlayerProfileId <= 0)
        {
            Debug.LogWarning("[ContextMenu] profileId=0, cannot send friend request!");
            CloseMenu();
            return; // Guard against invalid profile ID
        }

        if (addFriendButton == null || !addFriendButton.gameObject.activeSelf || !addFriendButton.interactable)
        {
            return;
        }

        int targetProfileId = currentPlayerProfileId;
        string targetPlayerName = currentPlayerName;
        friendStatusRequestVersion++;
        SetAddFriendLoading(true); // Disable button and show spinner

        FriendApi.SendFriendRequest(
            targetProfileId,
            _ =>
            {
                CachePendingRequest(targetProfileId); // Remember outgoing request locally
                Debug.Log($"[ContextMenu] Friend request sent -> {targetPlayerName} (id={targetProfileId})");
                if (currentPlayerProfileId == targetProfileId)
                {
                    SetAddFriendSent(); // Update button to "Sent" state
                }
            },
            err =>
            {
                Debug.LogWarning($"[ContextMenu] SendFriendRequest failed: {err?.Message}");
                if (IsPendingFriendRequestError(err))
                {
                    CachePendingRequest(targetProfileId);
                    if (currentPlayerProfileId == targetProfileId)
                    {
                        SetAddFriendSent();
                    }
                }
                else if (IsAlreadyFriendError(err))
                {
                    if (currentPlayerProfileId == targetProfileId)
                    {
                        HideAddFriendButton();
                    }
                }
                else if (currentPlayerProfileId == targetProfileId)
                {
                    ResetAddFriendButton();
                }
            });
    }

    // Executes on report clicked operation.
    private void OnReportClicked()
    {
        CloseMenu();
    }

    // Executes is pointer over this menu operation.
    private bool IsPointerOverThisMenu()
    {
        if (EventSystem.current != null)
        {
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            raycastResults.Clear();
            EventSystem.current.RaycastAll(eventData, raycastResults);

            foreach (var result in raycastResults)
            {
                if (result.gameObject != null && result.gameObject.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
        }

        Camera cam = null;
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            cam = parentCanvas.worldCamera;
        }

        return menuRect != null && RectTransformUtility.RectangleContainsScreenPoint(menuRect, Input.mousePosition, cam);
    }

    // Executes ensure button raycasts operation.
    private void EnsureButtonRaycasts()
    {
        EnsureButtonRaycast(viewProfileButton);
        EnsureButtonRaycast(addFriendButton);
        EnsureButtonRaycast(reportButton);
    }

    // Executes ensure button raycast operation.
    private static void EnsureButtonRaycast(Button button)
    {
        if (button == null)
            return;

        var graphic = button.targetGraphic as Graphic;
        if (graphic == null)
        {
            graphic = button.GetComponent<Graphic>();
            if (graphic != null)
            {
                button.targetGraphic = graphic;
            }
        }

        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }
    }

    // Executes ensure hover effects operation.
    private void EnsureHoverEffects()
    {
        EnsureHoverEffect(viewProfileButton);
        EnsureHoverEffect(addFriendButton);
        EnsureHoverEffect(reportButton);
    }

    // Executes ensure hover effect operation.
    private static void EnsureHoverEffect(Button button)
    {
        if (button == null) return;
        if (button.GetComponent<UIHoverScaleEffect>() == null)
        {
            button.gameObject.AddComponent<UIHoverScaleEffect>();
        }
    }

    // Executes refresh add friend visibility operation.
    private void RefreshAddFriendVisibility()
    {
        if (addFriendButton == null)
        {
            return;
        }

        if (IsSelf(currentPlayerProfileId, currentPlayerName))
        {
            HideAddFriendButton();
            return;
        }

        int requestVersion = ++friendStatusRequestVersion;

        // Check if target player is already in accepted friend list
        FriendApi.GetFriendList(
            friends =>
            {
                if (requestVersion != friendStatusRequestVersion || !gameObject.activeInHierarchy) return;

                bool isAlreadyFriend = friends != null && friends.Exists(f =>
                    f != null && (
                        (currentPlayerProfileId > 0 && f.FriendProfileId == currentPlayerProfileId) ||
                        (!string.IsNullOrEmpty(currentPlayerName) && string.Equals(f.FriendName?.Trim(), currentPlayerName.Trim(), StringComparison.OrdinalIgnoreCase))
                    ));

                if (isAlreadyFriend)
                {
                    RemoveCachedPendingRequest(currentPlayerProfileId);
                    HideAddFriendButton(); // Hide Add Friend button if already friends
                    return;
                }

                CheckSearchRelationshipStatus(requestVersion);
            },
            _ =>
            {
                if (requestVersion != friendStatusRequestVersion || !gameObject.activeInHierarchy) return;
                CheckSearchRelationshipStatus(requestVersion);
            });
    }

    private void CheckSearchRelationshipStatus(int requestVersion)
    {
        if (string.IsNullOrEmpty(currentPlayerName)) return;

        FriendApi.SearchPlayers(
            currentPlayerName,
            players =>
            {
                if (requestVersion != friendStatusRequestVersion || !gameObject.activeInHierarchy) return;
                ApplyFriendRelationshipState(players);
            },
            error =>
            {
                if (requestVersion != friendStatusRequestVersion || !gameObject.activeInHierarchy) return;
                if (HasCachedPendingRequest(currentPlayerProfileId))
                {
                    SetAddFriendSent();
                }
                else
                {
                    ResetAddFriendButton();
                }
            });
    }

    // Executes apply friend relationship state operation.
    private void ApplyFriendRelationshipState(List<FriendSearchDto> players)
    {
        FriendSearchDto target = null;
        if (players != null)
        {
            target = players.Find(player =>
                player != null && (
                    (currentPlayerProfileId > 0 && player.ProfileId == currentPlayerProfileId) ||
                    (!string.IsNullOrEmpty(currentPlayerName) && string.Equals(player.CharacterName?.Trim(), currentPlayerName.Trim(), StringComparison.OrdinalIgnoreCase))
                ));
        }

        if (target == null)
        {
            if (HasCachedPendingRequest(currentPlayerProfileId))
            {
                SetAddFriendSent();
            }
            else
            {
                ResetAddFriendButton();
            }
            return;
        }

        switch (target.RelationshipStatus)
        {
            case FriendRelationshipStatus.RequestSent:
                CachePendingRequest(currentPlayerProfileId);
                SetAddFriendSent();
                break;
            case FriendRelationshipStatus.RequestReceived:
                RemoveCachedPendingRequest(currentPlayerProfileId);
                SetAddFriendUnavailable("Request Received");
                break;
            case FriendRelationshipStatus.Friend:
            case FriendRelationshipStatus.Blocked:
            case FriendRelationshipStatus.Self:
                RemoveCachedPendingRequest(currentPlayerProfileId);
                HideAddFriendButton(); // Hide Add Friend button if already friends
                break;
            default:
                RemoveCachedPendingRequest(currentPlayerProfileId);
                ResetAddFriendButton();
                break;
        }
    }

    // Executes is already friend error operation.
    private static bool IsAlreadyFriendError(ApiException error)
    {
        string message = error?.Message ?? string.Empty;
        return message.IndexOf("already friends", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Executes is pending friend request error operation.
    private static bool IsPendingFriendRequestError(ApiException error)
    {
        string message = error?.Message ?? string.Empty;
        return message.IndexOf("request already sent", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("pending friend request", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Executes is self check operation.
    private static bool IsSelf(int profileId, string playerName)
    {
        if (profileId > 0 && IsCurrentPlayer(profileId)) return true;
        string myName = GetCurrentPlayerName();
        return !string.IsNullOrEmpty(playerName) && !string.IsNullOrEmpty(myName) &&
               string.Equals(playerName.Trim(), myName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // Executes get current player name operation.
    private static string GetCurrentPlayerName()
    {
        string name = GameStateService.Instance != null ? GameStateService.Instance.PlayerName : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = WorldState.PlayerName;
        }
        return name ?? string.Empty;
    }

    // Executes is current player operation.
    private static bool IsCurrentPlayer(int profileId)
    {
        return GetCurrentPlayerId() > 0 && profileId == GetCurrentPlayerId();
    }

    // Executes get current player id operation.
    private static int GetCurrentPlayerId()
    {
        int currentPlayerId = GameStateService.Instance != null
            ? GameStateService.Instance.PlayerProfileId
            : 0;

        if (currentPlayerId <= 0)
        {
            currentPlayerId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
        }

        return currentPlayerId;
    }

    // Executes get request key operation.
    private static long GetRequestKey(int targetProfileId)
    {
        return ((long)GetCurrentPlayerId() << 32) | (uint)targetProfileId;
    }

    // Executes has cached pending request operation.
    private static bool HasCachedPendingRequest(int targetProfileId)
    {
        return PendingOutgoingRequests.Contains(GetRequestKey(targetProfileId));
    }

    // Executes cache pending request operation.
    private static void CachePendingRequest(int targetProfileId)
    {
        PendingOutgoingRequests.Add(GetRequestKey(targetProfileId));
    }

    // Executes remove cached pending request operation.
    private static void RemoveCachedPendingRequest(int targetProfileId)
    {
        PendingOutgoingRequests.Remove(GetRequestKey(targetProfileId));
    }

    // Executes hide add friend button operation.
    private void HideAddFriendButton()
    {
        if (addFriendButton == null)
        {
            return;
        }

        addFriendButton.gameObject.SetActive(false);
        ReflowLayout();
    }
    // Executes set add friend loading operation.
    private void SetAddFriendLoading(bool loading)
    {
        if (addFriendButton == null) return;
        addFriendButton.gameObject.SetActive(true);
        addFriendButton.interactable = !loading;
        SetLabel(addFriendButton, loading ? "Sending..." : "Add Friend");
    }

    // Update add friend checking; it updates add friend unavailable.
    private void SetAddFriendChecking()
    {
        SetAddFriendUnavailable("Checking...");
    }

    // Executes set add friend unavailable operation.
    private void SetAddFriendUnavailable(string label = "Unavailable")
    {
        if (addFriendButton == null) return;
        addFriendButton.gameObject.SetActive(true);
        addFriendButton.interactable = false;
        SetLabel(addFriendButton, label);
    }

    // Executes set add friend sent operation.
    private void SetAddFriendSent()
    {
        if (addFriendButton == null) return;
        addFriendButton.gameObject.SetActive(true);
        addFriendButton.interactable = false;
        SetLabel(addFriendButton, "Request Sent");
    }

    // Executes reset add friend button operation.
    private void ResetAddFriendButton()
    {
        if (addFriendButton == null) return;
        addFriendButton.gameObject.SetActive(true);
        addFriendButton.interactable = true;
        SetLabel(addFriendButton, "Add Friend");
    }

    // Executes set label operation.
    private static void SetLabel(Button btn, string text)
    {
        var lbl = btn.GetComponentInChildren<TMP_Text>(true);
        if (lbl != null) lbl.text = text;
    }

    // Executes describe button operation.
    private static string DescribeButton(Button button)
    {
        if (button == null)
            return "null";

        return $"{button.name}, active={button.gameObject.activeInHierarchy}, interactable={button.interactable}";
    }
}
