using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// View for a SINGLE party slot. It is a dumb renderer: the controller
/// (<see cref="UIPartyPanel"/>) calls one of the Render* methods each time the
/// networked roster changes, and this component just pushes the values onto its UI
/// widgets. It holds NO party logic — clicks are forwarded via the callbacks passed in.
///
/// References are exposed for the Inspector so you can wire/inspect them per slot.
/// Any field left empty is auto-resolved by name in <see cref="Awake"/> (so the panel
/// still works if a reference is forgotten, matching the project's existing convention).
/// </summary>
public class UIPartySlot : MonoBehaviour
{
    [Header("Widgets (auto-found by name if left empty)")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject leaderIcon;
    [SerializeField] private GameObject readyIcon;
    [SerializeField] private GameObject notReadyIcon;
    [SerializeField] private GameObject podium;
    [SerializeField] private Button inviteButton;   // "+" on an empty slot
    [SerializeField] private Button kickButton;     // "X" on an occupied member slot

    private bool _resolved;

    private void Awake() => ResolveReferences();

    /// <summary>Fill empty Inspector fields by searching child objects by name.</summary>
    private void ResolveReferences()
    {
        if (_resolved) return;
        _resolved = true;

        if (avatarImage == null)
        {
            // Use a DEDICATED "Avatar" child only — never the Podium art, so setting the
            // class sprite does not overwrite the stone podium base.
            var av = transform.Find("Podium/Avatar") ?? transform.Find("Avatar");
            if (av != null) avatarImage = av.GetComponent<Image>();
        }
        if (nameText == null)
        {
            var n = transform.Find("MemberName") ?? transform.Find("Level");
            if (n != null) nameText = n.GetComponent<TMP_Text>();
        }
        if (leaderIcon == null)
        {
            var l = transform.Find("LeaderIcon");
            if (l != null) leaderIcon = l.gameObject;
        }
        if (readyIcon == null)
        {
            var r = transform.Find("Status/ReadyIcon");
            if (r != null) readyIcon = r.gameObject;
        }
        if (notReadyIcon == null)
        {
            var nr = transform.Find("Status/NotReadyIcon");
            if (nr != null) notReadyIcon = nr.gameObject;
        }
        if (podium == null)
        {
            var p = transform.Find("Podium");
            if (p != null) podium = p.gameObject;
        }
        if (inviteButton == null)
        {
            var ib = transform.Find("InviteButton");
            if (ib != null) inviteButton = ib.GetComponent<Button>();
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

    /// <summary>Render the host (slot leader): avatar + name, leader icon on, no kick/invite.</summary>
    public void RenderHost(string displayName, int level, CharacterClass cls, Sprite avatar)
    {
        ResolveReferences();
        Show(podium, true);
        Show(leaderIcon, true);
        SetAvatar(avatar, cls);
        SetName($"{displayName}\n<size=70%>Lv.{level} {cls}</size>");

        Show(readyIcon, true);       // host is always ready
        Show(notReadyIcon, false);
        Show(inviteButton, false);
        Show(kickButton, false);
        ClearListeners();
    }

    /// <summary>Render an occupied member slot with ready state + optional kick (host view).</summary>
    public void RenderMember(string displayName, int level, CharacterClass cls, Sprite avatar,
                             bool ready, bool canKick, Action onKick)
    {
        ResolveReferences();
        Show(podium, true);
        Show(leaderIcon, false);
        SetAvatar(avatar, cls);
        SetName($"{displayName}\n<size=70%>Lv.{level} {cls}</size>");

        Show(readyIcon, ready);
        Show(notReadyIcon, !ready);
        Show(inviteButton, false);

        Show(kickButton, canKick);
        ClearListeners();
        if (canKick && kickButton != null && onKick != null)
            kickButton.onClick.AddListener(() => onKick());
    }

    /// <summary>Render an empty slot: "+" invite button (host only), everything else hidden.</summary>
    public void RenderEmpty(bool canInvite, Action onInvite)
    {
        ResolveReferences();
        Show(podium, false);
        Show(leaderIcon, false);
        Show(readyIcon, false);
        Show(notReadyIcon, false);
        Show(kickButton, false);
        SetName(string.Empty);
        HideAvatar();

        Show(inviteButton, canInvite);
        ClearListeners();
        if (canInvite && inviteButton != null && onInvite != null)
        {
            inviteButton.interactable = true;
            inviteButton.onClick.AddListener(() => onInvite());
            var label = inviteButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "+";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Placeholder class colours for testing (until real portraits are wired).</summary>
    public static Color ClassColor(CharacterClass cls)
    {
        switch (cls)
        {
            case CharacterClass.Archer: return new Color(0.20f, 0.75f, 0.25f); // green
            case CharacterClass.Knight: return new Color(0.95f, 0.78f, 0.15f); // yellow
            case CharacterClass.Mage:   return new Color(0.25f, 0.45f, 0.95f); // blue
            default:                    return Color.white;
        }
    }

    private void ClearListeners()
    {
        if (inviteButton != null) inviteButton.onClick.RemoveAllListeners();
        if (kickButton != null) kickButton.onClick.RemoveAllListeners();
    }

    private void SetAvatar(Sprite sprite, CharacterClass cls)
    {
        if (avatarImage == null) return;
        avatarImage.enabled = true;
        if (sprite != null) avatarImage.sprite = sprite;
        // For testing we tint the avatar by class (green/yellow/blue). When real class
        // portraits are supplied via ClassAvatarDatabase, set the tint to Color.white
        // in the caller and rely on the sprite instead.
        avatarImage.color = ClassColor(cls);
    }

    private void HideAvatar()
    {
        // Empty slot: nothing to show. Podium is hidden anyway; keep the avatar quiet.
        if (avatarImage != null) avatarImage.enabled = false;
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
