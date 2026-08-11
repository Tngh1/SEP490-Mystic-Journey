using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPaperPopupView : MonoBehaviour
{
    public enum PaperPopupKind { None, Accepted, Completed, Claimed, AchievementUnlocked }

    [Header("Texts")]
    // TileText = dòng trạng thái động của quest hoặc achievement.
    [SerializeField] private TMP_Text titleTMP;
    // AnnounceText = tên quest/achievement hoặc nội dung thông báo.
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

    public void Show(string message, PaperPopupKind kind)
    {
        SetMessage(message);
        ApplyKind(kind);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

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
                stampAnimator.Rebind();
                stampAnimator.Update(0f);
            }
        }
    }

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
        // TileText = dòng trạng thái (title). Bind trước để fallback của messageTMP không chộp nhầm nó.
        if (titleTMP == null)
            titleTMP = FindTextMesh("TileText") ?? FindTextMesh("TitleText");

        if (messageTMP == null)
        {
            messageTMP = FindTextMesh("AnnounceText") ?? FindTextMesh("PopupText") ?? FindTextMesh("MessageText");
            // Fallback cuối: TMP con bất kỳ NHƯNG không phải TileText (tránh gộp title vào message).
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
