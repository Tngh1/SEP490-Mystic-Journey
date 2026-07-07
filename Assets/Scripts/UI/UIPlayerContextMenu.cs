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

public class UIPlayerContextMenu : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text playerNameText;

    [Header("Buttons (keo vao Inspector hoac de auto-find)")]
    public Button viewProfileButton;
    public Button addFriendButton;
    public Button reportButton;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private string currentPlayerName;
    private int currentPlayerProfileId;
    private float menuOpenTime = -1f;
    private int friendStatusRequestVersion;
    private const float ClickCooldown = 0.15f;

    private RectTransform menuRect;
    private Canvas parentCanvas;

    private void Awake()
    {
        menuRect = transform as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        AutoFindButtons();
        BindButtons();
        EnsureButtonRaycasts();
    }

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

    public void ShowMenu(string playerName, int playerProfileId, Vector3 position)
    {
        currentPlayerName = playerName;
        currentPlayerProfileId = playerProfileId;
        menuOpenTime = Time.unscaledTime;

        if (playerNameText != null)
            playerNameText.text = playerName;

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        AutoFindButtons();
        BindButtons();
        EnsureButtonRaycasts();
        ResetAddFriendButton();
        RefreshAddFriendVisibility();

        Debug.Log($"[ContextMenu] ShowMenu -> name={playerName} profileId={playerProfileId} addButton={DescribeButton(addFriendButton)}");
    }

    public void CloseMenu()
    {
        gameObject.SetActive(false);
    }

    private void OnViewProfileClicked()
    {
        Debug.Log($"[ContextMenu] ViewProfile clicked -> name={currentPlayerName} profileId={currentPlayerProfileId}");
        CloseMenu();
    }

    private void OnAddFriendClicked()
    {
        Debug.Log($"[ContextMenu] AddFriend clicked -> profileId={currentPlayerProfileId}");

        if (currentPlayerProfileId <= 0)
        {
            Debug.LogWarning("[ContextMenu] profileId=0, cannot send friend request!");
            CloseMenu();
            return;
        }

        if (addFriendButton != null && !addFriendButton.gameObject.activeSelf)
        {
            CloseMenu();
            return;
        }

        SetAddFriendLoading(true);

        FriendApi.SendFriendRequest(
            currentPlayerProfileId,
            _ =>
            {
                Debug.Log($"[ContextMenu] Friend request sent -> {currentPlayerName} (id={currentPlayerProfileId})");
                SetAddFriendSent();
            },
            err =>
            {
                Debug.LogWarning($"[ContextMenu] SendFriendRequest failed: {err?.Message}");
                if (IsAlreadyFriendError(err))
                {
                    HideAddFriendButton();
                }
                else
                {
                    ResetAddFriendButton();
                }
            });
    }

    private void OnReportClicked()
    {
        CloseMenu();
    }

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

    private void EnsureButtonRaycasts()
    {
        EnsureButtonRaycast(viewProfileButton);
        EnsureButtonRaycast(addFriendButton);
        EnsureButtonRaycast(reportButton);
    }

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
        FriendApi.GetFriendList(
            friends =>
            {
                if (requestVersion != friendStatusRequestVersion || !gameObject.activeInHierarchy)
                {
                    return;
                }

                ApplyFriendRelationshipState(friends);
            },
            error =>
            {
                if (requestVersion != friendStatusRequestVersion || !gameObject.activeInHierarchy)
                {
                    return;
                }

                Debug.LogWarning($"[ContextMenu] Cannot check friend status: {error?.Message}");
                ResetAddFriendButton();
            });
    }

    private void ApplyFriendRelationshipState(List<FriendDto> friends)
    {
        if (friends == null)
        {
            ResetAddFriendButton();
            return;
        }

        foreach (var friend in friends)
        {
            if (friend == null || friend.FriendProfileId != currentPlayerProfileId)
            {
                continue;
            }

            if (IsAcceptedStatus(friend.Status))
            {
                HideAddFriendButton();
                Debug.Log($"[ContextMenu] Hide AddFriend because target is already friend. profileId={currentPlayerProfileId}");
                return;
            }

            if (IsPendingStatus(friend.Status))
            {
                SetAddFriendSent();
                return;
            }
        }

        ResetAddFriendButton();
    }

    private static bool IsAcceptedStatus(string status)
    {
        return string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Friend", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPendingStatus(string status)
    {
        return string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "RequestSent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Request Sent", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAlreadyFriendError(ApiException error)
    {
        string message = error?.Message ?? string.Empty;
        string code = error?.ErrorCode ?? string.Empty;
        return code.IndexOf("FRIEND", StringComparison.OrdinalIgnoreCase) >= 0
            && code.IndexOf("EXIST", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0
            && message.IndexOf("friend", StringComparison.OrdinalIgnoreCase) >= 0;
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

    private void HideAddFriendButton()
    {
        if (addFriendButton == null)
        {
            return;
        }

        addFriendButton.gameObject.SetActive(false);
    }
    private void SetAddFriendLoading(bool loading)
    {
        if (addFriendButton == null) return;
        addFriendButton.gameObject.SetActive(true);
        addFriendButton.interactable = !loading;
        SetLabel(addFriendButton, loading ? "Sending..." : "Add Friend");
    }

    private void SetAddFriendSent()
    {
        if (addFriendButton == null) return;
        addFriendButton.gameObject.SetActive(true);
        addFriendButton.interactable = false;
        SetLabel(addFriendButton, "Request Sent");
    }

    private void ResetAddFriendButton()
    {
        if (addFriendButton == null) return;
        addFriendButton.gameObject.SetActive(true);
        addFriendButton.interactable = true;
        SetLabel(addFriendButton, "Add Friend");
    }

    private static void SetLabel(Button btn, string text)
    {
        var lbl = btn.GetComponentInChildren<TMP_Text>(true);
        if (lbl != null) lbl.text = text;
    }

    private static string DescribeButton(Button button)
    {
        if (button == null)
            return "null";

        return $"{button.name}, active={button.gameObject.activeInHierarchy}, interactable={button.interactable}";
    }
}