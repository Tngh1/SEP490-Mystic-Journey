using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// View for a SINGLE party podium slot. Matches the current UI design:
///   • Podium      — stone base (always visible while the slot exists).
///   • Flag        — class banner; sprite swaps per class (Knight/Mage/Archer).
///   • Name        — class name-plate IMAGE; sprite also swaps per class. Its TMP
///                   child shows the player's display name + level.
///   • LeaderIcon  — crown shown only on the host slot.
///   • Avatar      — portrait of the member's equipped skin (hidden when unresolved).
///   • Ready       — check badge shown when a member is ready (member slots only).
///   • KickButton  — remove a member (shown to the host on member slots only).
///
/// It is a dumb renderer: <see cref="UIPartyPanel"/> calls one Render* method per roster
/// change and this pushes values onto the widgets. It holds NO party logic — the kick
/// click is forwarded via the callback passed in.
///
/// Empty fields are auto-resolved by child name in <see cref="ResolveReferences"/>.
/// </summary>
public class UIPartySlot : MonoBehaviour
{
    [Header("Widgets (auto-found by name if left empty)")]
    [SerializeField] private Image flagImage;      // "Flag" — class banner
    [SerializeField] private Image nameplateImage; // "Name" — class name label (image)
    [SerializeField] private TMP_Text nameText;    // TMP under "Name"
    [SerializeField] private Image avatarImage;    // optional portrait
    [SerializeField] private GameObject leaderIcon;
    [SerializeField] private GameObject readyIcon; // "Ready" — check badge (member slots)
    [SerializeField] private GameObject podium;
    [SerializeField] private Button kickButton;    // "KickButton" — host removes a member

    private bool _resolved;

    private void Awake() => ResolveReferences();

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
            // The visible name banner is "Name/Background" (drawn on top of the parent
            // "Name" image). Prefer it so class art actually shows; fall back to "Name".
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

    // ─────────────────────────────────────────────────────────────────────────
    // Render states — called by the controller
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Render the host (leader): class art + name, crown on, no ready badge / kick.</summary>
    public void RenderHost(string displayName, int level, CharacterClass cls,
                           Sprite classFlag, Sprite classNameplate, Sprite skinPortrait = null)
    {
        ResolveReferences();
        Show(podium, true);
        Show(leaderIcon, true);
        Show(readyIcon, false);   // host has no ready badge (always ready implicitly)
        Show(kickButton, false);
        ClearListeners();
        SetClassArt(cls, classFlag, classNameplate, skinPortrait);
        SetName($"{displayName}\n<size=70%>Lv.{level}</size>");
    }

    /// <summary>Render an occupied member slot: class art, ready badge, and a kick button
    /// visible only when the local player is the host.</summary>
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

    /// <summary>Render an empty slot: only the bare podium, everything else hidden.</summary>
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

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ClearListeners()
    {
        if (kickButton != null) kickButton.onClick.RemoveAllListeners();
    }

    /// <summary>Swap the Flag + Name-plate sprites to the member's class art, and the
    /// portrait to their equipped skin (same preview sprite the inventory shows).</summary>
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
                // Skin previews are tall character frames; without this they stretch
                // to fill the square Avatar rect.
                avatarImage.preserveAspect = true;
            }
        }
    }

    private void HideAvatar()
    {
        if (avatarImage != null)
        {
            avatarImage.enabled = false;
            avatarImage.gameObject.SetActive(false);
        }
    }

    private void SetName(string text)
    {
        if (nameText != null) nameText.text = text;
    }

    private static void Show(Component c, bool on)
    {
        if (c != null) c.gameObject.SetActive(on);
    }

    private static void Show(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }
}
