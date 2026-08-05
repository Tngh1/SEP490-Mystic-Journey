using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Confirmation box backed by the Main Scene's <c>Canvas/PopupLayer/PartyPopup</c> — the popup the
/// designers made, as opposed to <see cref="MysticJourney.UI.UIPopupManager"/> (the generic dialog).
///
/// PartyPopup carries no MonoBehaviour in the scene and both OkButton/CloseButton have empty
/// onClick lists, so the label and every click has to be bound from code. Centralised here because
/// two callers now need it (party kick, logout) and both would otherwise hit the same two traps
/// documented below.
/// </summary>
public static class PartyPopupConfirm
{
    /// <summary>
    /// Message-only variant: OK and Close both just dismiss. Party feedback ("Invited X.", "party is
    /// full", ...) used to go through <c>WorldRuntimeEvents.RaiseMessage</c>, which has no subscriber,
    /// so every one of those strings was silently dropped. Routing them to the quest popup instead is
    /// wrong: its Kind.None branch stamps the title "Quest not complete" over whatever it is given.
    /// </summary>
    public static void Notify(Transform caller, string message) => Show(caller, message, null);

    /// <summary>
    /// Opens the confirmation popup and runs <paramref name="onConfirm"/> if the player presses OK.
    /// If the popup cannot be found, <paramref name="onConfirm"/> runs immediately: losing the
    /// confirmation step beats leaving the caller's button dead.
    /// </summary>
    /// <param name="caller">Any UI node living under the same Canvas as PopupLayer.</param>
    public static void Show(Transform caller, string message, Action onConfirm)
    {
        // PartyPopup is a child of PopupLayer — a different branch from the calling panel
        // (Canvas/PartyPanel, Canvas/PlayerProfilePanel, ...), so we walk up to the Canvas and back
        // down. Anchoring on the Canvas rather than transform.parent keeps this working if a panel
        // ever gains an extra wrapper.
        var canvas = caller != null ? caller.GetComponentInParent<Canvas>() : null;
        var popup = canvas != null ? canvas.transform.Find("PopupLayer/PartyPopup") : null;
        if (popup == null)
        {
            Debug.LogWarning("[PartyPopupConfirm] Canvas/PopupLayer/PartyPopup missing; continuing without confirmation.");
            onConfirm?.Invoke();
            return;
        }

        var text = popup.Find("Text")?.GetComponent<TMP_Text>();
        if (text != null) text.text = message;

        var okButton = popup.Find("OkButton")?.GetComponent<Button>();
        var closeButton = popup.Find("CloseButton")?.GetComponent<Button>();

        // RemoveAllListeners is mandatory: one PartyPopup instance is reused by every caller, so
        // without it the previous listener survives — open the kick popup for A, close it, then
        // open the logout popup, and a single OK press kicks A as well.
        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(() =>
            {
                popup.gameObject.SetActive(false);
                onConfirm?.Invoke();
            });
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => popup.gameObject.SetActive(false));
        }

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

        // With neither button bound the popup hangs with no way to dismiss it — warn so that shows
        // up immediately instead of as a silently stuck UI.
        if (okButton == null && closeButton == null)
            Debug.LogWarning("[PartyPopupConfirm] PartyPopup has neither OkButton nor CloseButton; popup cannot be dismissed.");
    }
}
