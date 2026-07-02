using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIChatPanel : MonoBehaviour
{
    [Header("Chat UI")]
    public ScrollRect scrollRect;
    public Transform contentParent;
    public TMP_InputField inputField;
    public Button sendButton;
    
    [Header("Message Prefab")]
    public UIChatMessage chatMessagePrefab;

    [Header("Colors")]
    public Color myNameColor = Color.yellow;
    public Color otherNameColor = Color.cyan;

    private void Start()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendClicked);
        }

        if (inputField != null)
        {
            inputField.onSubmit.AddListener((text) => OnSendClicked());
        }
    }

    public void OnSendClicked()
    {
        if (inputField == null || string.IsNullOrWhiteSpace(inputField.text))
        {
            return;
        }

        string msg = inputField.text;
        inputField.text = ""; // clear input

        // Local echo immediately (later this should be sent to server first)
        AddMessage("You", msg, true);
        
        // Return focus to input field
        inputField.ActivateInputField();
    }

    public void AddMessage(string sender, string message, bool isMe)
    {
        if (chatMessagePrefab == null || contentParent == null) return;

        UIChatMessage newMsg = Instantiate(chatMessagePrefab, contentParent);
        Color senderColor = isMe ? myNameColor : otherNameColor;
        
        // Transparent background
        newMsg.Setup(sender, message, senderColor, new Color(0,0,0,0));

        // Wait for UI layout to rebuild, then scroll to bottom
        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        // Wait a frame so layout groups can recalculate heights
        yield return new WaitForEndOfFrame();
        
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
