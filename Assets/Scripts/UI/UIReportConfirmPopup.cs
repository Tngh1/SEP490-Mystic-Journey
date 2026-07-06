using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

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

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
            
        if (cancelButton != null)
            cancelButton.onClick.AddListener(ClosePopup);
            
        if (backgroundBlockerButton != null)
            backgroundBlockerButton.onClick.AddListener(ClosePopup);
    }

    public void ShowPopup(string targetDescription, Action onConfirm)
    {
        onConfirmAction = onConfirm;
        
        if (messageText != null)
        {
            messageText.text = $"Are you sure you want to report {targetDescription} for inappropriate language?\n\nOur system will review the chat logs carefully.";
        }
        
        gameObject.SetActive(true);
    }

    private void OnConfirmClicked()
    {
        onConfirmAction?.Invoke();
        ClosePopup();
    }

    public void ClosePopup()
    {
        gameObject.SetActive(false);
    }
}
