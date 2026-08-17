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

        if (playerNameText != null)
            playerNameText.text = playerName; // Display target player name on header

        transform.SetAsLastSibling(); // Bring menu to front
        gameObject.SetActive(true); // Open modal
        AutoFindButtons();
        BindButtons();
        EnsureButtonRaycasts();
        EnsureHoverEffects();
        if (HasCachedPendingRequest(currentPlayerProfileId))
        {
            SetAddFriendSent(); // Update button label to "Pending"
        }
        else
        {
            SetAddFriendChecking(); // Set loading spinner while querying friendship relation
        }
        RefreshAddFriendVisibility(); // Query FriendApi to check if already friends

        Debug.Log($"[ContextMenu] ShowMenu -> name={playerName} profileId={playerProfileId} addButton={DescribeButton(addFriendButton)}");
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

        if (currentPlayerProfileId <= 0 || IsCurrentPlayer(currentPlayerProfileId))
        {
            HideAddFriendButton();
            return;
        }

        int requestVersion = ++friendStatusRequestVersion;
        FriendApi.SearchPlayers(
            currentPlayerName,
            players =>
            {
                if (requestVersion != friendStatusRequestVersion || !gameObject.activeInHierarchy)
                {
                    return;
                }

                ApplyFriendRelationshipState(players);
            },
            error =>
            {
                if (requestVersion != friendStatusRequestVersion || !gameObject.activeInHierarchy)
                {
                    return;
                }

                Debug.LogWarning($"[ContextMenu] Cannot check friend status: {error?.Message}");
                if (HasCachedPendingRequest(currentPlayerProfileId))
                {
                    SetAddFriendSent();
                }
                else
                {
                    SetAddFriendUnavailable();
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
                player != null && player.ProfileId == currentPlayerProfileId);
        }

        if (target == null)
        {
            if (HasCachedPendingRequest(currentPlayerProfileId))
            {
                SetAddFriendSent();
            }
            else
            {
                SetAddFriendUnavailable();
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
                HideAddFriendButton();
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
