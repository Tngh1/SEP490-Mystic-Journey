using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIQuestPopupView : MonoBehaviour
{
    public enum QuestPopupKind { None, Claimed, Completed }

    [SerializeField] private TMP_Text messageTMP;
    [SerializeField] private Text messageText;

    [Header("Status Icons")]
    [SerializeField] private GameObject claimedIcon;
    [SerializeField] private GameObject questCompletedIcon;
    [SerializeField] private Animator stampAnimator;

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
        Show(message, InferKind(message));
    }

    public void Show(string message, QuestPopupKind kind)
    {
        SetMessage(message);
        ApplyKind(kind);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    private void ApplyKind(QuestPopupKind kind)
    {
        Bind();
        if (claimedIcon != null) claimedIcon.SetActive(kind == QuestPopupKind.Claimed);
        if (questCompletedIcon != null) questCompletedIcon.SetActive(kind == QuestPopupKind.Completed);

        if (kind != QuestPopupKind.None && stampAnimator != null)
        {
            stampAnimator.gameObject.SetActive(true);
            stampAnimator.Rebind();
            stampAnimator.Update(0f);
        }
    }

    // ponytail: infer icon from message text so existing string-only call sites keep working.
    // Upgrade path: pass QuestPopupKind explicitly from callers if messages get localized.
    // Claimed icon = accept quest ("Quest Accepted!"); Completed icon = finish ("Quest completed!").
    private static QuestPopupKind InferKind(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return QuestPopupKind.None;
        var lower = message.ToLowerInvariant();
        if (lower.Contains("accept")) return QuestPopupKind.Claimed;
        if (lower.Contains("complete")) return QuestPopupKind.Completed;
        return QuestPopupKind.None;
    }

    public void Hide()
    {
        if (claimedIcon != null) claimedIcon.SetActive(false);
        if (questCompletedIcon != null) questCompletedIcon.SetActive(false);
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
            messageTMP = FindTextMesh("AnnounceText") ?? FindTextMesh("PopupText") ?? FindTextMesh("MessageText") ?? GetComponentInChildren<TMP_Text>(true);

        if (stampAnimator == null)
        {
            var stamp = FindDescendant(transform, "Stamp");
            if (stamp != null) stampAnimator = stamp.GetComponent<Animator>();
        }

        if (messageText == null)
            messageText = FindText("PopupText") ?? FindText("MessageText") ?? GetComponentInChildren<Text>(true);

        if (claimedIcon == null)
            claimedIcon = FindDescendant(transform, "Claimed");
        if (questCompletedIcon == null)
            questCompletedIcon = FindDescendant(transform, "QuestCompleted");
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
