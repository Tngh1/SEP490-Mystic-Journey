using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class MainNpcPanelRuntime : MonoBehaviour
{
    public static MainNpcPanelRuntime Instance { get; private set; }

    [Header("Scene UI")]
    [SerializeField] private GameObject npcPanel;
    [SerializeField] private Image portraitImage;
    [SerializeField] private QuestImageLibrary imageLibrary;

    private static readonly Dictionary<string, Sprite> RemoteSprites = new Dictionary<string, Sprite>();

    private TextSlot nameText;
    private TextSlot roleText;
    private TextSlot dialogueText;
    private TextSlot questHintText;
    private GameObject actionButtonObject;
    private Button actionButton;
    private Button closeButton;
    private int firstQuestId;
    private Coroutine imageRoutine;
    private bool didBind;

    public bool IsOpen => npcPanel != null && npcPanel.activeInHierarchy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private IEnumerator Start()
    {
        yield return null;
        BindUi();
    }

    private void OnDestroy()
    {
        if (imageRoutine != null)
            StopCoroutine(imageRoutine);

        if (Instance == this)
            Instance = null;
    }

    public void OpenForNpc(WorldInteractable interactable)
    {
        if (interactable == null)
            return;

        BindUi();
        if (npcPanel == null)
            return;

        RenderLocal(interactable);
        ShowPanel();

        if (!ApiClient.Instance.HasToken() || interactable.NpcId <= 0)
            return;

        var manager = GetQuestManager();
        if (manager == null)
        {
            Debug.LogWarning("[MainNpcPanelRuntime] QuestManager was not found in Main scene.");
            return;
        }

        manager.TalkToNpc(
            interactable.NpcId,
            response => RenderApiResponse(response, interactable),
            error =>
            {
                SetText(dialogueText, string.IsNullOrWhiteSpace(interactable.GreetingText) ? error : interactable.GreetingText);
                Debug.LogWarning($"[MainNpcPanelRuntime] TalkToNpc failed: {error}");
            }
        );
    }

    private void BindUi()
    {
        npcPanel = npcPanel != null ? npcPanel : FindSceneObject("NPCPanel");
        if (npcPanel == null)
        {
            if (!didBind)
                Debug.LogWarning("[MainNpcPanelRuntime] NPCPanel was not found in Main scene.");
            didBind = true;
            return;
        }

        portraitImage = portraitImage != null ? portraitImage : FindPortraitImage();
        nameText = nameText.IsValid ? nameText : FindTextSlot(npcPanel.transform, "NpcNameText", "NPCNameText", "NameText", "TitleText");
        roleText = roleText.IsValid ? roleText : FindTextSlot(npcPanel.transform, "NpcRoleText", "RoleText", "DescriptionText", skip: nameText);

        var dialogueArea = FindDescendant(npcPanel.transform, "DialogueTextArea")?.transform ?? npcPanel.transform;
        dialogueText = dialogueText.IsValid ? dialogueText : FindTextSlot(dialogueArea, "DialogueText", "DialogText", "ContentText", "Text (TMP)");
        if (!dialogueText.IsValid)
            dialogueText = FindTextSlot(npcPanel.transform, "DialogueText", "DialogText", "ContentText", "Text (TMP)", skip: nameText, skip2: roleText);

        var actionArea = FindDescendant(npcPanel.transform, "ActionArea")?.transform ?? npcPanel.transform;
        questHintText = questHintText.IsValid ? questHintText : FindTextSlot(actionArea, "QuestHintText", "HintText", "QuestText", skip: nameText, skip2: roleText, skip3: dialogueText);

        actionButtonObject = actionButtonObject != null ? actionButtonObject :
                             FindDescendant(actionArea, "OpenQuestButton") ??
                             FindDescendant(actionArea, "QuestButton") ??
                             FindDescendant(actionArea, "ActionButton") ??
                             actionArea.gameObject;
        actionButton = BindButton(actionButtonObject, OpenFirstQuest);

        var closeObject = FindDescendant(npcPanel.transform, "CloseNpcButton") ?? FindDescendant(npcPanel.transform, "CloseButton");
        closeButton = BindButton(closeObject, ClosePanel);

        SetActionVisible(false);
        npcPanel.SetActive(false);
        didBind = true;
    }

    private void RenderLocal(WorldInteractable interactable)
    {
        firstQuestId = interactable.QuestId ?? 0;
        SetText(nameText, Safe(interactable.DisplayName, "Elder Rowan"));
        SetText(roleText, Safe(interactable.Description, "Tutorial elder and main quest giver."));
        SetText(dialogueText, Safe(interactable.GreetingText, "Welcome to ElfLand. Talk to me when you are ready for your first quest."));
        SetText(questHintText, firstQuestId > 0 ? "Quest available" : string.Empty);
        SetActionVisible(firstQuestId > 0);
    }

    private void RenderApiResponse(TalkToNpcResponse response, WorldInteractable fallback)
    {
        var npc = response?.Npc;
        var linkedQuests = response?.LinkedQuests?
            .Where(q => q != null && !QuestManager.IsStatus(q, "Claimed"))
            .OrderBy(q => QuestManager.IsStatus(q, "InProgress") ? 0 : QuestManager.IsStatus(q, "Completed") ? 1 : 2)
            .ThenBy(q => q.RequiredLevel)
            .ThenBy(q => q.QuestId)
            .ToList() ?? new List<PlayerQuestResponse>();

        firstQuestId = linkedQuests.FirstOrDefault()?.QuestId ?? 0;

        SetText(nameText, Safe(npc?.Name, fallback.DisplayName));
        SetText(roleText, Safe(npc?.Description, fallback.Description));
        SetText(dialogueText, BuildDialogue(response, fallback, linkedQuests));
        SetText(questHintText, BuildQuestHint(linkedQuests));
        SetActionVisible(firstQuestId > 0);
        ApplyPortrait(npc);
    }

    private string BuildDialogue(TalkToNpcResponse response, WorldInteractable fallback, List<PlayerQuestResponse> linkedQuests)
    {
        var dialogues = response?.Npc?.Dialogues?
            .Where(d => d != null && d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ToList() ?? new List<NPCDialogueResponse>();

        var lines = new List<string>();
        var intro = dialogues.FirstOrDefault(d => !d.LinkedQuestId.HasValue);
        var questDialogue = PickQuestDialogue(dialogues, linkedQuests);

        if (!string.IsNullOrWhiteSpace(intro?.Content))
            lines.Add(intro.Content);
        if (!string.IsNullOrWhiteSpace(questDialogue?.Content) && questDialogue != intro)
            lines.Add(questDialogue.Content);

        if (lines.Count == 0)
            lines.Add(Safe(fallback.GreetingText, "Welcome to ElfLand. Talk to me when you are ready for your first quest."));

        return string.Join("\n\n", lines);
    }

    private static NPCDialogueResponse PickQuestDialogue(List<NPCDialogueResponse> dialogues, List<PlayerQuestResponse> linkedQuests)
    {
        if (dialogues == null || dialogues.Count == 0)
            return null;

        if (linkedQuests != null && linkedQuests.Count > 0)
        {
            var questIds = linkedQuests.Select(q => q.QuestId).ToHashSet();
            var linked = dialogues.FirstOrDefault(d => d.LinkedQuestId.HasValue && questIds.Contains(d.LinkedQuestId.Value));
            if (linked != null)
                return linked;
        }

        return dialogues.FirstOrDefault(d => d.LinkedQuestId.HasValue);
    }

    private static string BuildQuestHint(List<PlayerQuestResponse> linkedQuests)
    {
        if (linkedQuests == null || linkedQuests.Count == 0)
            return "No linked quest available.";

        var quest = linkedQuests[0];
        var status = string.IsNullOrWhiteSpace(quest.Status) ? "Available" : quest.Status;
        return $"{quest.QuestTitle} [{status}]";
    }

    private void OpenFirstQuest()
    {
        if (firstQuestId <= 0)
            return;

        var questPanelRuntime = MainQuestPanelRuntime.Instance ?? FindQuestPanelRuntime();
        if (questPanelRuntime != null)
            questPanelRuntime.OpenQuestPanelForQuest(firstQuestId);
        else if (UIManager.Instance != null)
            UIManager.Instance.OpenQuestPanel();
    }

    private void ShowPanel()
    {
        WorldInteractionPromptRuntime.Hide();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowPanel(npcPanel);
        else
            npcPanel.SetActive(true);
    }

    private void ClosePanel()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(npcPanel);
        else if (npcPanel != null)
            npcPanel.SetActive(false);
    }

    private void SetActionVisible(bool visible)
    {
        if (actionButtonObject != null)
            actionButtonObject.SetActive(visible);
        if (actionButton != null)
            actionButton.interactable = visible;
    }

    private void ApplyPortrait(NPCResponse npc)
    {
        if (portraitImage == null || npc == null)
            return;

        var local = GetLibrarySprite($"npc:{npc.NPCId}", npc.Name);
        if (local != null)
        {
            portraitImage.sprite = local;
            portraitImage.enabled = true;
        }

        if (string.IsNullOrWhiteSpace(npc.IconUrl))
            return;

        if (imageRoutine != null)
            StopCoroutine(imageRoutine);
        imageRoutine = StartCoroutine(LoadSprite(npc.IconUrl, portraitImage));
    }

    private IEnumerator LoadSprite(string rawUrl, Image target)
    {
        var url = ResolveUrl(rawUrl);
        if (string.IsNullOrWhiteSpace(url) || target == null)
            yield break;

        if (RemoteSprites.TryGetValue(url, out var cached) && cached != null)
        {
            target.sprite = cached;
            target.enabled = true;
            yield break;
        }

        using var request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[MainNpcPanelRuntime] Load NPC icon failed: {request.error}");
            yield break;
        }

        var texture = DownloadHandlerTexture.GetContent(request);
        if (texture == null)
            yield break;

        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        RemoteSprites[url] = sprite;

        if (target != null)
        {
            target.sprite = sprite;
            target.enabled = true;
        }
    }

    private Sprite GetLibrarySprite(params string[] ids)
    {
        if (imageLibrary == null || ids == null)
            return null;

        for (var i = 0; i < ids.Length; i++)
        {
            var sprite = imageLibrary.GetSprite(ids[i]);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private Image FindPortraitImage()
    {
        var portrait = FindDescendant(npcPanel.transform, "PortraitSlot");
        if (portrait == null)
            return null;

        return portrait.GetComponent<Image>() ?? portrait.GetComponentInChildren<Image>(true);
    }

    private static QuestManager GetQuestManager()
    {
        if (QuestManager.Instance != null)
            return QuestManager.Instance;

        var managers = Resources.FindObjectsOfTypeAll<QuestManager>();
        for (var i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager != null && manager.gameObject.scene.IsValid() && manager.gameObject.scene.name == "Main")
                return manager;
        }

        return null;
    }
    private static MainQuestPanelRuntime FindQuestPanelRuntime()
    {
        var runtimes = Resources.FindObjectsOfTypeAll<MainQuestPanelRuntime>();
        for (var i = 0; i < runtimes.Length; i++)
        {
            var runtime = runtimes[i];
            if (runtime != null && runtime.gameObject.scene.IsValid() && runtime.gameObject.scene.name == "Main")
                return runtime;
        }

        return null;
    }

    private static Button BindButton(GameObject target, UnityEngine.Events.UnityAction action)
    {
        if (target == null)
            return null;

        var button = target.GetComponent<Button>();
        if (button == null)
            button = target.AddComponent<Button>();
        if (button == null)
            return null;

        if (button.onClick == null)
            button.onClick = new Button.ButtonClickedEvent();

        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(action);
        return button;
    }

    private static string ResolveUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var trimmed = rawUrl.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.StartsWith("/"))
            return ApiConfig.BaseUrl.TrimEnd('/') + trimmed;

        return ApiConfig.BaseUrl.TrimEnd('/') + "/" + trimmed;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj != null && obj.name == objectName && obj.scene.IsValid() && obj.scene.name == "Main")
                return obj;
        }

        return null;
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

    private static TextSlot FindTextSlot(Transform root, string name1, string name2 = null, string name3 = null, string name4 = null, TextSlot skip = default, TextSlot skip2 = default, TextSlot skip3 = default)
    {
        if (root == null)
            return default;

        var names = new[] { name1, name2, name3, name4 }.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        for (var i = 0; i < names.Length; i++)
        {
            var child = FindDescendant(root, names[i]);
            var slot = TextSlot.From(child);
            if (slot.IsValid && !slot.Equals(skip) && !slot.Equals(skip2) && !slot.Equals(skip3))
                return slot;
        }

        var tmps = root.GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < tmps.Length; i++)
        {
            var slot = new TextSlot(tmps[i], null);
            if (!slot.Equals(skip) && !slot.Equals(skip2) && !slot.Equals(skip3))
                return slot;
        }

        var texts = root.GetComponentsInChildren<Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var slot = new TextSlot(null, texts[i]);
            if (!slot.Equals(skip) && !slot.Equals(skip2) && !slot.Equals(skip3))
                return slot;
        }

        return default;
    }

    private static void SetText(TextSlot slot, string value)
    {
        slot.Set(value);
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private readonly struct TextSlot : IEquatable<TextSlot>
    {
        private readonly TMP_Text tmp;
        private readonly Text text;

        public TextSlot(TMP_Text tmp, Text text)
        {
            this.tmp = tmp;
            this.text = text;
        }

        public bool IsValid => tmp != null || text != null;

        public static TextSlot From(GameObject target)
        {
            if (target == null)
                return default;

            return new TextSlot(target.GetComponent<TMP_Text>(), target.GetComponent<Text>());
        }

        public void Set(string value)
        {
            if (tmp != null)
            {
                tmp.text = value ?? string.Empty;
                return;
            }

            if (text != null)
                text.text = value ?? string.Empty;
        }

        public bool Equals(TextSlot other)
        {
            return tmp == other.tmp && text == other.text;
        }

        public override bool Equals(object obj)
        {
            return obj is TextSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((tmp != null ? tmp.GetHashCode() : 0) * 397) ^ (text != null ? text.GetHashCode() : 0);
            }
        }
    }
}

