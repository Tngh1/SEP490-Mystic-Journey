using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIQuestPopupView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageTMP;
    [SerializeField] private Text messageText;

    private void Awake()
    {
        Bind();
    }

    private void OnValidate()
    {
        Bind();
    }

    public void Show(string message)
    {
        SetMessage(message);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetMessage(string message)
    {
        Bind();

        if (messageTMP != null)
        {
            messageTMP.text = message ?? string.Empty;
            return;
        }

        if (messageText != null)
            messageText.text = message ?? string.Empty;
    }

    private void Bind()
    {
        if (messageTMP == null)
            messageTMP = FindTextMesh("PopupText") ?? FindTextMesh("MessageText") ?? GetComponentInChildren<TMP_Text>(true);

        if (messageText == null)
            messageText = FindText("PopupText") ?? FindText("MessageText") ?? GetComponentInChildren<Text>(true);
    }

    private TMP_Text FindTextMesh(string objectName)
    {
        var child = FindDescendant(transform, objectName);
        return child == null ? null : child.GetComponent<TMP_Text>();
    }

    private Text FindText(string objectName)
    {
        var child = FindDescendant(transform, objectName);
        return child == null ? null : child.GetComponent<Text>();
    }

    private static GameObject FindDescendant(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        var all = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                return all[i].gameObject;
        }

        return null;
    }
}
