using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// Executes mono behaviour operation.
public class UIReportConfirmPopup : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text titleText;
    public TMP_Text messageText;

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    [Header("Background Blocker")]
    public Button backgroundBlockerButton;

    private Action onConfirmAction;

    // Initializes internal component caches and dependencies for UIReportConfirmPopup upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(ClosePopup);

        if (backgroundBlockerButton != null)
            backgroundBlockerButton.onClick.AddListener(ClosePopup);
    }

    // Executes show popup operation.
    public void ShowPopup(string targetDescription, Action onConfirm)
    {
        onConfirmAction = onConfirm;

        if (messageText != null)
        {
            messageText.text = $"Are you sure you want to report {targetDescription} for inappropriate language?\n\nOur system will review the chat logs carefully.";
        }

        gameObject.SetActive(true);
    }

    // Executes on confirm clicked operation.
    private void OnConfirmClicked()
    {
        onConfirmAction?.Invoke();
        ClosePopup();
    }

    // Update visibility for popup; it updates active.
    public void ClosePopup()
    {
        gameObject.SetActive(false);
    }
}
