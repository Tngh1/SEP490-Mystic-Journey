using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Executes mono behaviour operation.
public class PartyInvitePopup : MonoBehaviour
{
    // Executes invite operation.
    private struct Invite
    {
        public int HostProfileId;
        public string HostName;
    }

    private static PartyInvitePopup _instance;

    private readonly Queue<Invite> _queue = new();
    private bool _showing;

    private GameObject _root;
    private TMP_Text _messageText;


    // Executes ensure exists operation.
    public static void EnsureExists()
    {
        if (_instance != null) return;
        var go = new GameObject("PartyInvitePopup");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PartyInvitePopup>();
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        PlayerPresence.OnInviteReceived += HandleInvite;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        PlayerPresence.OnInviteReceived -= HandleInvite;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }


    // Executes handle invite operation.
    private void HandleInvite(int hostProfileId, string hostName)
    {
        if (PartyService.CurrentParty != null)
        {
            PartyService.DeclineInvite(hostProfileId);
            return;
        }

        _queue.Enqueue(new Invite { HostProfileId = hostProfileId, HostName = hostName });
        if (!_showing) ShowNext();
    }

    // Executes show next operation.
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

    // Executes on accept operation.
    private void OnAccept()
    {
        if (_queue.Count == 0) return;
        var invite = _queue.Dequeue();
        PartyService.AcceptInvite(invite.HostProfileId);
        ShowNext();
    }

    // Executes on decline operation.
    private void OnDecline()
    {
        if (_queue.Count == 0) return;
        var invite = _queue.Dequeue();
        PartyService.DeclineInvite(invite.HostProfileId);
        ShowNext();
    }


    // Executes build ui operation.
    private void BuildUI()
    {
        _root = new GameObject("InvitePopupCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(_root);
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(_root.transform, false);
        frame.GetComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 0.97f);
        var frameRt = frame.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(1f, 0f);
        frameRt.anchorMax = new Vector2(1f, 0f);
        frameRt.pivot = new Vector2(1f, 0f);
        frameRt.anchoredPosition = new Vector2(-30, 30);
        frameRt.sizeDelta = new Vector2(380, 150);

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
            // Process vector2 using transform, parent, name, and label; it builds button, updates parent, loads component, and creates listener.
            new Vector2(0f, 0f), new Vector2(20, 18), OnAccept);
        BuildButton(frame.transform, "DeclineBtn", "DECLINE", new Color(0.55f, 0.2f, 0.2f),
            // Process vector2 using parent, name, label, and color; it builds button, updates parent, loads component, and creates listener.
            new Vector2(1f, 0f), new Vector2(-20, 18), OnDecline);
    }

    // Derive button using parent, name, label, and color; it updates parent, loads component, and creates listener.
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
