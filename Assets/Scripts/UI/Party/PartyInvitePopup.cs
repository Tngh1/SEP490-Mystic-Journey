using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Listens for incoming party invites (<see cref="PlayerPresence.OnInviteReceived"/>)
/// and shows a simple "Player XXX invited you [Accept] [Decline]" popup in the Main
/// scene. Contains NO business logic — Accept/Decline just call <see cref="PartyService"/>.
///
/// Self-bootstrapping: a single instance is created on demand under the Main canvas
/// the first time an invite arrives, so no prefab wiring is required. Multiple invites
/// queue and are shown one at a time.
///
/// The popup UI is built in code to match the existing convention in
/// <see cref="UIPartyPanel"/> (which also builds its modal at runtime).
/// </summary>
public class PartyInvitePopup : MonoBehaviour
{
    private struct Invite
    {
        public PlayerRef Host;
        public int HostProfileId;
        public string HostName;
    }

    private static PartyInvitePopup _instance;

    private readonly Queue<Invite> _queue = new();
    private bool _showing;

    private GameObject _root;
    private TMP_Text _messageText;

    // ─────────────────────────────────────────────────────────────────────────
    // Bootstrap
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensure a listener exists. Called from Main-scene bootstrap once the player is in.
    /// Idempotent. The listener survives scene changes so invites still arrive while
    /// browsing menus.
    /// </summary>
    public static void EnsureExists()
    {
        if (_instance != null) return;
        var go = new GameObject("PartyInvitePopup");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PartyInvitePopup>();
    }

    private void OnEnable()
    {
        PlayerPresence.OnInviteReceived += HandleInvite;
    }

    private void OnDisable()
    {
        PlayerPresence.OnInviteReceived -= HandleInvite;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Invite handling
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleInvite(PlayerRef host, int hostProfileId, string hostName)
    {
        // Ignore invites while already in a party (would need to leave first).
        if (PartyService.CurrentParty != null)
        {
            PartyService.DeclineInvite(host);
            return;
        }

        _queue.Enqueue(new Invite { Host = host, HostProfileId = hostProfileId, HostName = hostName });
        if (!_showing) ShowNext();
    }

    private void ShowNext()
    {
        if (_queue.Count == 0)
        {
            _showing = false;
            if (_root != null) _root.SetActive(false);
            return;
        }

        _showing = true;
        var invite = _queue.Peek();

        if (_root == null) BuildUI();
        _root.SetActive(true);
        _messageText.text = $"<b>{invite.HostName}</b> invited you to a party.";
    }

    private void OnAccept()
    {
        if (_queue.Count == 0) return;
        var invite = _queue.Dequeue();
        PartyService.AcceptInvite(invite.Host);
        ShowNext();
    }

    private void OnDecline()
    {
        if (_queue.Count == 0) return;
        var invite = _queue.Dequeue();
        PartyService.DeclineInvite(invite.Host);
        ShowNext();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI construction (runtime, no prefab)
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Own overlay canvas so the popup renders above everything regardless of scene.
        _root = new GameObject("InvitePopupCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(_root);
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Frame anchored bottom-right (toast style, non-blocking).
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(_root.transform, false);
        frame.GetComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 0.97f);
        var frameRt = frame.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(1f, 0f);
        frameRt.anchorMax = new Vector2(1f, 0f);
        frameRt.pivot = new Vector2(1f, 0f);
        frameRt.anchoredPosition = new Vector2(-30, 30);
        frameRt.sizeDelta = new Vector2(380, 150);

        // Message
        var msgObj = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        msgObj.transform.SetParent(frame.transform, false);
        _messageText = msgObj.GetComponent<TextMeshProUGUI>();
        _messageText.fontSize = 18;
        _messageText.color = Color.white;
        _messageText.alignment = TextAlignmentOptions.TopLeft;
        var msgRt = msgObj.GetComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0, 1);
        msgRt.anchorMax = new Vector2(1, 1);
        msgRt.pivot = new Vector2(0.5f, 1f);
        msgRt.anchoredPosition = new Vector2(0, -14);
        msgRt.offsetMin = new Vector2(16, msgRt.offsetMin.y);
        msgRt.offsetMax = new Vector2(-16, msgRt.offsetMax.y);
        msgRt.sizeDelta = new Vector2(msgRt.sizeDelta.x, 70);

        BuildButton(frame.transform, "AcceptBtn", "ACCEPT", new Color(0.18f, 0.55f, 0.22f),
            new Vector2(0f, 0f), new Vector2(20, 18), OnAccept);
        BuildButton(frame.transform, "DeclineBtn", "DECLINE", new Color(0.55f, 0.2f, 0.2f),
            new Vector2(1f, 0f), new Vector2(-20, 18), OnDecline);
    }

    private void BuildButton(Transform parent, string name, string label, Color color,
                             Vector2 anchor, Vector2 offset, UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        btnObj.GetComponent<Image>().color = color;
        var rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(160, 40);

        var btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        var txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);
        var txt = txtObj.GetComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 15;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        var txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
    }
}
