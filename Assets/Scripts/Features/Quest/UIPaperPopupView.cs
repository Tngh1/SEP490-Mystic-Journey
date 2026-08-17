using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class UIPaperPopupView : MonoBehaviour
{
    // Executes paper popup kind operation.
    public enum PaperPopupKind { None, Accepted, Completed, Claimed, AchievementUnlocked }

    [Header("Texts")]
    [SerializeField] private TMP_Text titleTMP;
    [SerializeField] private TMP_Text messageTMP;
    [SerializeField] private Text messageText;

    [Header("Status Icons")]
    [SerializeField] private GameObject claimedIcon;
    [SerializeField] private GameObject questCompletedIcon;
    [SerializeField] private Animator stampAnimator;

    // Initializes internal component caches and dependencies for UIPaperPopupView upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        Bind();
    }

    // Executes on validate operation.
    private void OnValidate()
    {
        Bind();
    }

    // Executes show operation.
    public void Show(string message)
    {
        Show(message, InferKind(message));
    }

    // Executes show operation.
    public void Show(string message, PaperPopupKind kind)
    {
        SetMessage(message);
        ApplyKind(kind);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    // Executes apply kind operation.
    private void ApplyKind(PaperPopupKind kind)
    {
        Bind();

        if (titleTMP != null)
        {
            titleTMP.text = kind switch
            {
                PaperPopupKind.Accepted => "Quest Accepted!",
                PaperPopupKind.Completed => "Quest Completed!",
                PaperPopupKind.Claimed => "Reward Claimed!",
                PaperPopupKind.AchievementUnlocked => "Achievement Unlocked!",
                _ => "Notification",
            };
        }

        if (claimedIcon != null)
            claimedIcon.SetActive(kind == PaperPopupKind.Accepted ||
                                  kind == PaperPopupKind.Claimed ||
                                  kind == PaperPopupKind.AchievementUnlocked);
        if (questCompletedIcon != null)
            questCompletedIcon.SetActive(kind == PaperPopupKind.Completed);

        if (stampAnimator != null)
        {
            if (kind == PaperPopupKind.None)
            {
                stampAnimator.gameObject.SetActive(false);
            }
            else
            {
                stampAnimator.gameObject.SetActive(true);
                if (stampAnimator.gameObject.activeInHierarchy)
                {
                    stampAnimator.Rebind();
                    stampAnimator.Update(0f);
                }
            }
        }
    }

    // Executes infer kind operation.
    // Validates input parameters against null or empty values.
    private static PaperPopupKind InferKind(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return PaperPopupKind.None;
        var lower = message.ToLowerInvariant();
        if (lower.Contains("achievement")) return PaperPopupKind.AchievementUnlocked;
        if (lower.Contains("accept")) return PaperPopupKind.Accepted;
        if (lower.Contains("complete")) return PaperPopupKind.Completed;
        if (lower.Contains("claim") || lower.Contains("reward")) return PaperPopupKind.Claimed;
        return PaperPopupKind.None;
    }


    // Executes hide operation.
    public void Hide()
    {
        if (claimedIcon != null) claimedIcon.SetActive(false);
        if (questCompletedIcon != null) questCompletedIcon.SetActive(false);
        gameObject.SetActive(false);
    }

    // Executes set message operation.
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

    // Executes bind operation.
    private void Bind()
    {
        if (titleTMP == null)
            titleTMP = FindTextMesh("TileText") ?? FindTextMesh("TitleText");

        if (messageTMP == null)
        {
            messageTMP = FindTextMesh("AnnounceText") ?? FindTextMesh("PopupText") ?? FindTextMesh("MessageText");
            if (messageTMP == null)
            {
                foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp != null && tmp != titleTMP) { messageTMP = tmp; break; }
                }
            }
        }

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

    // Executes find text mesh operation.
    private TMP_Text FindTextMesh(string objectName)
    {
        var child = FindDescendant(transform, objectName);
        return child == null ? null : child.GetComponent<TMP_Text>();
    }

    // Executes find text operation.
    // Validates input parameters against null or empty values.
    private Text FindText(string objectName)
    {
        var child = FindDescendant(transform, objectName);
        return child == null ? null : child.GetComponent<Text>();
    }

    // Executes find descendant operation.
    // Validates input parameters against null or empty values.
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
