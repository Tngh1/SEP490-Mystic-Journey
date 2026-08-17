using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Executes mono behaviour operation.
public class UIPartySlot : MonoBehaviour
{
    [Header("Widgets (auto-found by name if left empty)")]
    [SerializeField] private Image flagImage;
    [SerializeField] private Image nameplateImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private GameObject leaderIcon;
    [SerializeField] private GameObject readyIcon;
    [SerializeField] private GameObject podium;
    [SerializeField] private Button kickButton;

    private bool _resolved;

    // Initializes internal component caches and dependencies for UIPartySlot upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake() => ResolveReferences();

    // Executes resolve references operation.
    private void ResolveReferences()
    {
        if (_resolved) return;
        _resolved = true;

        if (flagImage == null)
        {
            var f = transform.Find("Flag");
            if (f != null) flagImage = f.GetComponent<Image>();
        }
        if (nameplateImage == null)
        {
            var bg = transform.Find("Name/Background");
            var n = transform.Find("Name");
            if (bg != null) nameplateImage = bg.GetComponent<Image>();
            else if (n != null) nameplateImage = n.GetComponent<Image>();
        }
        if (nameText == null)
        {
            var n = transform.Find("Name");
            if (n != null) nameText = n.GetComponentInChildren<TMP_Text>(true);
        }
        if (avatarImage == null)
        {
            var av = transform.Find("Podium/Avatar") ?? transform.Find("Avatar");
            if (av != null) avatarImage = av.GetComponent<Image>();
        }
        if (leaderIcon == null)
        {
            var l = transform.Find("LeaderIcon");
            if (l != null) leaderIcon = l.gameObject;
        }
        if (readyIcon == null)
        {
            var r = transform.Find("Ready") ?? transform.Find("ReadyIcon");
            if (r != null) readyIcon = r.gameObject;
        }
        if (podium == null)
        {
            var p = transform.Find("Podium");
            if (p != null) podium = p.gameObject;
        }
        if (kickButton == null)
        {
            var kb = transform.Find("KickButton");
            if (kb != null) kickButton = kb.GetComponent<Button>();
        }
    }


    // Render or refresh host using display name, level, cls, and class flag; it builds references, updates navigation or visibility through show, updates class art, and updates name.
    public void RenderHost(string displayName, int level, CharacterClass cls,
                           Sprite classFlag, Sprite classNameplate, Sprite skinPortrait = null)
    {
        ResolveReferences();
        Show(podium, true);
        Show(leaderIcon, true);
        Show(readyIcon, false);
        Show(kickButton, false);
        ClearListeners();
        SetClassArt(cls, classFlag, classNameplate, skinPortrait);
        SetName($"{displayName}\n<size=70%>Lv.{level}</size>");
    }

    // Render or refresh member using display name, level, cls, and class flag; it builds references, updates navigation or visibility through show, updates class art, updates name, and creates listener and guards invalid or unavailable states.
    public void RenderMember(string displayName, int level, CharacterClass cls,
                             Sprite classFlag, Sprite classNameplate,
                             bool ready, bool canKick, Action onKick, Sprite skinPortrait = null)
    {
        ResolveReferences();
        Show(podium, true);
        Show(leaderIcon, false);
        SetClassArt(cls, classFlag, classNameplate, skinPortrait);
        SetName($"{displayName}\n<size=70%>Lv.{level}</size>");

        Show(readyIcon, ready);

        ClearListeners();
        Show(kickButton, canKick);
        if (canKick && kickButton != null && onKick != null)
        {
            kickButton.interactable = true;
            kickButton.onClick.AddListener(() => onKick());
        }
    }

    // Executes render empty operation.
    public void RenderEmpty()
    {
        ResolveReferences();
        Show(podium, true);
        Show(leaderIcon, false);
        Show(readyIcon, false);
        Show(kickButton, false);
        Show(flagImage, false);
        Show(nameplateImage, false);
        SetName(string.Empty);
        HideAvatar();
        ClearListeners();
    }


    // Executes clear listeners operation.
    private void ClearListeners()
    {
        if (kickButton != null) kickButton.onClick.RemoveAllListeners();
    }

    // Executes set class art operation.
    private void SetClassArt(CharacterClass cls, Sprite classFlag, Sprite classNameplate, Sprite skinPortrait)
    {
        if (flagImage != null)
        {
            Show(flagImage, classFlag != null);
            if (classFlag != null) { flagImage.sprite = classFlag; flagImage.color = Color.white; }
        }
        if (nameplateImage != null)
        {
            Show(nameplateImage, true);
            if (classNameplate != null) { nameplateImage.sprite = classNameplate; nameplateImage.color = Color.white; }
        }

        if (avatarImage != null)
        {
            avatarImage.gameObject.SetActive(skinPortrait != null);
            avatarImage.enabled = skinPortrait != null;
            if (skinPortrait != null)
            {
                avatarImage.sprite = skinPortrait;
                avatarImage.color = Color.white;
                avatarImage.preserveAspect = true;
            }
        }
    }

    // Executes hide avatar operation.
    private void HideAvatar()
    {
        if (avatarImage != null)
        {
            avatarImage.enabled = false;
            avatarImage.gameObject.SetActive(false);
        }
    }

    // Executes set name operation.
    private void SetName(string text)
    {
        if (nameText != null) nameText.text = text;
    }

    // Executes show operation.
    private static void Show(Component c, bool on)
    {
        if (c != null) c.gameObject.SetActive(on);
    }

    // Executes show operation.
    private static void Show(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }
}
