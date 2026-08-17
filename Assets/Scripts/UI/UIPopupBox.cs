using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Initializes a new default instance of the UIPopupBox class.
public static class UIPopupBox
{
    // Process the supplied values: maps the input discriminator to the corresponding domain value and fallback.
    public static bool Notify(Transform caller, string title, string message, Action onOk = null,
        string okText = "OK", bool autoClose = true) =>
        ShowInternal(caller, title, message, onOk, null, okText, null, false, autoClose);

    // Process the supplied values: maps the input discriminator to the corresponding domain value and fallback.
    public static bool Show(Transform caller, string titleText, string message, Action onConfirm,
        Action onCancel = null, string confirmText = null, string cancelText = null,
        bool autoClose = true) =>
        ShowInternal(caller, titleText, message, onConfirm, onCancel, confirmText, cancelText,
            true, autoClose);

    // Update visibility for internal using caller, title text, message, and on confirm; it loads popup, loads find, loads component, updates active, and loads component in children and guards invalid or unavailable states and processes each matching entry.
    private static bool ShowInternal(Transform caller, string titleText, string message,
        Action onConfirm, Action onCancel, string confirmText, string cancelText,
        bool isDecision, bool autoClose)
    {
        var popup = FindPopup(caller);

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

        Bind(acceptButton, popup, onConfirm, autoClose);
        Bind(cancelButton, popup, onCancel, autoClose);
        Bind(closeButton, popup, onCancel, autoClose);

        for (var current = popup.parent; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf) current.gameObject.SetActive(true);
        }

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

        var popupCanvas = popup.GetComponent<Canvas>();
        if (popupCanvas == null) popupCanvas = popup.gameObject.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 9999;

        if (popup.GetComponent<GraphicRaycaster>() == null)
            popup.gameObject.AddComponent<GraphicRaycaster>();

        popup.gameObject.SetActive(true);
        popup.SetAsLastSibling();

        if (acceptButton == null && cancelButton == null && closeButton == null)
            Debug.LogWarning("[UIPopupBox] UIPopup has no usable buttons; popup cannot be dismissed.");

        return true;
    }

    // Executes find popup operation.
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

    // Executes hide operation.
    public static void Hide(Transform caller = null)
    {
        var popup = FindPopup(caller);
        if (popup != null) popup.gameObject.SetActive(false);
    }

    // Executes find child recursive operation.
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

    // Executes set active operation.
    private static void SetActive(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }

    // Executes bind operation.
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
    // Executes ui popup operation.
    public sealed class UIPopup
    {
        private static readonly UIPopup _instance = new UIPopup();
        // Executes instance operation.
        public static UIPopup Instance => _instance;

        // Executes popup container operation.
        public GameObject PopupContainer => global::UIPopupBox.FindPopup()?.gameObject;
        // Executes btn confirm operation.
        public Button BtnConfirm => global::UIPopupBox.FindPopup()?.Find("ConfirmButton")?.GetComponent<Button>();

        // Executes ui popup operation.
        private UIPopup() { }

        // Update visibility for alert using title, message, on ok, and ok text and returns the computed result.
        public void ShowAlert(string title, string message, Action onOk = null,
            string okText = "OK", bool autoClose = true)
        {
            global::UIPopupBox.Notify(null, title, message, onOk, okText, autoClose);
        }

        // Update visibility for confirm using title, message, on confirm, and on cancel; it updates navigation or visibility through show.
        public void ShowConfirm(string title, string message, Action onConfirm,
            Action onCancel = null, string confirmText = "Yes", string cancelText = "No",
            bool autoClose = true)
        {
            global::UIPopupBox.Show(null, title, message, onConfirm, onCancel,
                confirmText, cancelText, autoClose);
        }

        // Update visibility for popup; it updates navigation or visibility through hide.
        public void HidePopup() => global::UIPopupBox.Hide();
    }
}
