using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Confirmation / message box backed by the Main Scene's <c>Canvas/PopupLayer/UIPopup</c> — the popup
/// the designers made, as opposed to <see cref="MysticJourney.UI.UIPopupManager"/> (the generic dialog).
///
/// UIPopup carries no MonoBehaviour in the scene and all four buttons have empty onClick lists, so the
/// labels and every click have to be bound from code. Centralised here because several callers now need
/// it (party kick, logout, party feedback) and each would otherwise hit the same traps documented below.
///
/// The popup has two mutually exclusive layouts, picked by whether a confirm callback was supplied:
/// <list type="bullet">
/// <item>OkButton alone, centred — a message the player only acknowledges (<see cref="Notify"/>).</item>
/// <item>ConfirmButton + CancelButton at x=±150 — a decision (<see cref="Show"/>).</item>
/// </list>
/// OkButton is 200 wide and centred, so it overlaps each of the other two by 50px. Showing both
/// layouts at once puts unclickable button halves under each other; the visibility below is a
/// correctness requirement, not styling.
/// </summary>
public static class UIPopupBox
{
    /// <summary>
    /// Message-only variant: OK and the X both just dismiss. Party feedback ("Invited X.", "party is
    /// full", ...) used to go through <c>WorldRuntimeEvents.RaiseMessage</c>, which has no subscriber,
    /// so every one of those strings was silently dropped. Routing them to the quest popup instead is
    /// wrong: its Kind.None branch stamps the title "Quest not complete" over whatever it is given.
    /// </summary>
    public static void Notify(Transform caller, string title, string message) => Show(caller, title, message, null);

    /// <summary>
    /// Opens the popup and runs <paramref name="onConfirm"/> if the player accepts. Passing null makes
    /// it a message box. If the popup cannot be found, <paramref name="onConfirm"/> runs immediately:
    /// losing the confirmation step beats leaving the caller's button dead.
    /// </summary>
    /// <param name="caller">Any UI node living under the same Canvas as PopupLayer.</param>
    public static void Show(Transform caller, string titleText, string message, Action onConfirm, Action onCancel = null, string confirmText = null, string cancelText = null)
    {
        // UIPopup is a child of PopupLayer — a different branch from the calling panel
        // (Canvas/PartyPanel, Canvas/PlayerProfilePanel, ...), so we walk up to the Canvas and back
        // down. Anchoring on the Canvas rather than transform.parent keeps this working if a panel
        // ever gains an extra wrapper.
        var canvas = caller != null ? caller.GetComponentInParent<Canvas>() : null;
        var popup = canvas != null ? canvas.transform.Find("PopupLayer/UIPopup") : null;

        // Không có caller (vd. NetworkReconnectManager gọi Show(caller: null, ...)), hoặc caller
        // không nằm dưới Canvas gốc: FindObjectOfType<Canvas>() cũ trả về Canvas ĐẦU TIÊN Unity
        // gặp, mà scene luôn có hàng chục HealthBarCanvas/OverheadUI (mỗi quái/player một cái) —
        // một trong số đó đứng trước Canvas gốc khiến Find("PopupLayer/UIPopup") luôn null, popup
        // "Reconnecting..." không bao giờ hiện và onConfirm chạy ngay (Return to Menu → logout
        // thẳng, đúng triệu chứng mất kết nối mà không thấy panel thông báo). Quét toàn bộ Canvas
        // và chọn đúng cái sở hữu PopupLayer/UIPopup.
        if (popup == null)
        {
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                popup = c.transform.Find("PopupLayer/UIPopup");
                if (popup != null) break;
            }
        }

        if (popup == null)
        {
            Debug.LogWarning("[UIPopupBox] Canvas/PopupLayer/UIPopup missing; continuing without confirmation.");
            onConfirm?.Invoke();
            return;
        }

        bool isDecision = onConfirm != null || onCancel != null;

        var okButton = popup.Find("OkButton")?.GetComponent<Button>();
        var confirmButton = popup.Find("ConfirmButton")?.GetComponent<Button>();
        var cancelButton = popup.Find("CancelButton")?.GetComponent<Button>();
        var closeButton = popup.Find("CloseButton")?.GetComponent<Button>();

        // If the Confirm/Cancel pair is ever removed from the scene, fall back to OK rather than
        // dropping the decision: a logout button that does nothing is worse than one that asks
        // through a differently-labelled button.
        var acceptButton = isDecision ? (confirmButton ?? okButton) : okButton;

        SetActive(okButton, okButton == acceptButton);
        SetActive(confirmButton, confirmButton == acceptButton);
        SetActive(cancelButton, isDecision);

        if (acceptButton != null)
        {
            var txt = acceptButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = confirmText ?? (acceptButton == okButton ? "OK" : "Confirm");
        }
        if (cancelButton != null)
        {
            var txt = cancelButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = cancelText ?? "Cancel";
        }

        var title = popup.Find("Title")?.GetComponent<TMP_Text>();
        if (title != null) title.text = titleText;

        var text = popup.Find("Text")?.GetComponent<TMP_Text>();
        if (text != null) text.text = message;

        // RemoveAllListeners is mandatory: one UIPopup instance is reused by every caller, so
        // without it the previous listener survives — open the kick popup for A, close it, then
        // open the logout popup, and a single accept press kicks A as well.
        Bind(acceptButton, popup, onConfirm);
        Bind(cancelButton, popup, onCancel);
        Bind(closeButton, popup, onCancel);

        // SetActive(true) on the popup alone is not enough while PopupLayer is off: activeSelf turns
        // true but activeInHierarchy stays false, so nothing appears and no error is logged to trace
        // it back from. Re-enable inactive ancestors first (same as UIPopupManager and
        // MainMapPanelRuntime).
        for (var current = popup.parent; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf) current.gameObject.SetActive(true);
            if (current.GetComponent<Canvas>() != null) break;
        }

        popup.gameObject.SetActive(true);
        popup.SetAsLastSibling();

        // With no dismiss path the popup hangs on screen — warn so that shows up immediately instead
        // of as a silently stuck UI.
        if (acceptButton == null && cancelButton == null && closeButton == null)
            Debug.LogWarning("[UIPopupBox] UIPopup has no usable buttons; popup cannot be dismissed.");
    }

    private static void SetActive(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }

    /// <summary>Closes the popup, then runs <paramref name="onClick"/> if one was given.</summary>
    private static void Bind(Button button, Transform popup, Action onClick)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            popup.gameObject.SetActive(false);
            onClick?.Invoke();
        });
    }
}
