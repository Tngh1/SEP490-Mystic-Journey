using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Confirmation / message box backed by the Main Scene's <c>Canvas/PopupLayer/UIPopup</c> — the popup
/// the designers made. <see cref="MysticJourney.UI.UIPopup"/> exposes the same shared popup to
/// systems that do not have a convenient caller transform.
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
    /// so every one of those strings was silently dropped. PaperPopup remains a transient notification
    /// queue, while this component is the dedicated message/confirmation dialog.
    public static bool Notify(Transform caller, string title, string message, Action onOk = null,
        string okText = "OK", bool autoClose = true) =>
        ShowInternal(caller, title, message, onOk, null, okText, null, false, autoClose);

    /// <summary>
    /// Opens the popup and runs <paramref name="onConfirm"/> if the player accepts. Passing null makes
    /// it a message box. If the popup cannot be found, <paramref name="onConfirm"/> runs immediately:
    /// losing the confirmation step beats leaving the caller's button dead. Returns true if successfully shown.
    /// </summary>
    /// <param name="caller">Any UI node living under the same Canvas as PopupLayer.</param>
    public static bool Show(Transform caller, string titleText, string message, Action onConfirm,
        Action onCancel = null, string confirmText = null, string cancelText = null,
        bool autoClose = true) =>
        ShowInternal(caller, titleText, message, onConfirm, onCancel, confirmText, cancelText,
            true, autoClose);

    private static bool ShowInternal(Transform caller, string titleText, string message,
        Action onConfirm, Action onCancel, string confirmText, string cancelText,
        bool isDecision, bool autoClose)
    {
        // UIPopup is a child of PopupLayer — a different branch from the calling panel
        // (Canvas/PartyPanel, Canvas/PlayerProfilePanel, ...), so we walk up to the Canvas and back
        // down. Anchoring on the Canvas rather than transform.parent keeps this working if a panel
        // ever gains an extra wrapper.
        var popup = FindPopup(caller);

        // Không có caller (vd. NetworkReconnectManager gọi Show(caller: null, ...)), hoặc caller
        // không nằm dưới Canvas gốc: FindObjectOfType<Canvas>() cũ trả về Canvas ĐẦU TIÊN Unity
        // gặp, mà scene luôn có hàng chục HealthBarCanvas/OverheadUI (mỗi quái/player một cái) —
        // một trong số đó đứng trước Canvas gốc khiến Find("PopupLayer/UIPopup") luôn null, popup
        // "Reconnecting..." không bao giờ hiện và onConfirm chạy ngay (Return to Menu → logout
        // thẳng, đúng triệu chứng mất kết nối mà không thấy panel thông báo). Quét toàn bộ Canvas
        // và chọn đúng cái sở hữu PopupLayer/UIPopup.
        if (popup == null)
        {
            Debug.LogWarning("[UIPopupBox] Canvas/PopupLayer/UIPopup missing; continuing without confirmation.");
            onConfirm?.Invoke();
            return false;
        }

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
        SetActive(closeButton, !isDecision);

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

        // In the designed hierarchy the title is nested under BGTitle. Recursive lookup
        // prevents the serialized placeholder "New Text" from leaking into Logout and
        // other confirmations.
        var title = FindChildRecursive(popup, "Title")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            title.gameObject.SetActive(true);
            title.enabled = true;
            title.text = titleText;
        }

        var text = FindChildRecursive(popup, "Text")?.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.gameObject.SetActive(true);
            text.enabled = true;
            text.text = message;
        }

        // RemoveAllListeners is mandatory: one UIPopup instance is reused by every caller, so
        // without it the previous listener survives — open the kick popup for A, close it, then
        // open the logout popup, and a single accept press kicks A as well.
        Bind(acceptButton, popup, onConfirm, autoClose);
        Bind(cancelButton, popup, onCancel, autoClose);
        Bind(closeButton, popup, onCancel, autoClose);

        // SetActive(true) on the popup alone is not enough while PopupLayer is off: activeSelf turns
        // true but activeInHierarchy stays false, so nothing appears and no error is logged to trace
        // it back from. Re-enable inactive ancestors first (same as MapUIManager).
        // Bật toàn bộ các cấp cha bị ẩn (bao gồm PopupLayer)
        for (var current = popup.parent; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf) current.gameObject.SetActive(true);
        }

        // Đưa PopupLayer lên tầng trên cùng của Canvas (SetAsLastSibling) và gán SortingOrder = 9999
        if (popup.parent != null)
        {
            popup.parent.SetAsLastSibling();

            var parentCanvas = popup.parent.GetComponent<Canvas>();
            if (parentCanvas == null) parentCanvas = popup.parent.gameObject.AddComponent<Canvas>();
            parentCanvas.overrideSorting = true;
            parentCanvas.sortingOrder = 9999;

            if (popup.parent.GetComponent<GraphicRaycaster>() == null)
                popup.parent.gameObject.AddComponent<GraphicRaycaster>();
        }

        // Ép UIPopup override sorting order lên 9999 tuyệt đối
        var popupCanvas = popup.GetComponent<Canvas>();
        if (popupCanvas == null) popupCanvas = popup.gameObject.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 9999;

        if (popup.GetComponent<GraphicRaycaster>() == null)
            popup.gameObject.AddComponent<GraphicRaycaster>();

        popup.gameObject.SetActive(true);
        popup.SetAsLastSibling();

        // With no dismiss path the popup hangs on screen — warn so that shows up immediately instead
        // of as a silently stuck UI.
        if (acceptButton == null && cancelButton == null && closeButton == null)
            Debug.LogWarning("[UIPopupBox] UIPopup has no usable buttons; popup cannot be dismissed.");
            
        return true;
    }

    public static Transform FindPopup(Transform caller = null)
    {
        var canvas = caller != null ? caller.GetComponentInParent<Canvas>() : null;
        var popup = canvas != null ? canvas.transform.Find("PopupLayer/UIPopup") : null;

        if (popup != null) return popup;

        foreach (var candidate in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            popup = candidate.transform.Find("PopupLayer/UIPopup");
            if (popup != null) return popup;
        }

        return null;
    }

    public static void Hide(Transform caller = null)
    {
        var popup = FindPopup(caller);
        if (popup != null) popup.gameObject.SetActive(false);
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == childName) return child;

            var nested = FindChildRecursive(child, childName);
            if (nested != null) return nested;
        }

        return null;
    }

    private static void SetActive(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }

    /// <summary>Closes the popup, then runs <paramref name="onClick"/> if one was given.</summary>
    private static void Bind(Button button, Transform popup, Action onClick, bool autoClose)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (autoClose) popup.gameObject.SetActive(false);
            onClick?.Invoke();
        });
    }
}

namespace MysticJourney.UI
{
    /// <summary>
    /// Canonical controller for the designer-authored Canvas/PopupLayer/UIPopup.
    /// It intentionally requires no manager GameObject in the scene.
    /// </summary>
    public sealed class UIPopup
    {
        private static readonly UIPopup _instance = new UIPopup();
        public static UIPopup Instance => _instance;

        public GameObject PopupContainer => global::UIPopupBox.FindPopup()?.gameObject;
        public Button BtnConfirm => global::UIPopupBox.FindPopup()?.Find("ConfirmButton")?.GetComponent<Button>();

        private UIPopup() { }

        public void ShowAlert(string title, string message, Action onOk = null,
            string okText = "OK", bool autoClose = true)
        {
            global::UIPopupBox.Notify(null, title, message, onOk, okText, autoClose);
        }

        public void ShowConfirm(string title, string message, Action onConfirm,
            Action onCancel = null, string confirmText = "Yes", string cancelText = "No",
            bool autoClose = true)
        {
            global::UIPopupBox.Show(null, title, message, onConfirm, onCancel,
                confirmText, cancelText, autoClose);
        }

        public void HidePopup() => global::UIPopupBox.Hide();
    }
}
