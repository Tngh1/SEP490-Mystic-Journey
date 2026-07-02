using UnityEngine;
using TMPro;

public class UIChatMessage : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text senderText;
    public TMP_Text messageText;
    
    // Optional: background to color differently based on channel (world/guild/friend)
    public UnityEngine.UI.Image background;

    public void Setup(string sender, string message, Color senderColor, Color bgColor)
    {
        if (senderText != null)
        {
            senderText.text = sender + ":";
            senderText.color = senderColor;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (background != null && bgColor.a > 0)
        {
            background.color = bgColor;
        }
    }
}
