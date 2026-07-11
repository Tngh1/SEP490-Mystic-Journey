using System.Collections.Generic;
using UnityEngine;

public enum WorldInteractableKind
{
    Npc,
    Object,
    QuestItem,
    Dungeon
}

public class WorldInteractable : MonoBehaviour
{
    [SerializeField] private WorldInteractableKind kind = WorldInteractableKind.Object;
    [SerializeField] private int npcId;
    [SerializeField] private string displayName = "Interactable";
    [SerializeField] private string description = string.Empty;
    [TextArea]
    [SerializeField] private string greetingText = string.Empty;
    [SerializeField] private float interactionRadius = 2.25f;
    [SerializeField] private string objectKey = string.Empty;
    [SerializeField] private string interactionType = "Interact";
    [SerializeField] private int questId;
    [SerializeField] private int progressDelta = 1;
    [SerializeField] private int[] linkedQuestIds = new int[0];

    public WorldInteractableKind Kind => kind;
    public int NpcId => npcId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    public string Description => description;
    public string GreetingText => greetingText;
    public float InteractionRadius => Mathf.Max(0.5f, interactionRadius);
    public string ObjectKey => string.IsNullOrWhiteSpace(objectKey) ? gameObject.name : objectKey;
    public string InteractionType => string.IsNullOrWhiteSpace(interactionType) ? "Interact" : interactionType;
    public int? QuestId => questId > 0 ? questId : null;
    public int ProgressDelta => Mathf.Max(1, progressDelta);
    public IReadOnlyList<int> LinkedQuestIds => linkedQuestIds;

    public void ConfigureNpc(int id, string npcName, string npcDescription, string greeting, float radius, IEnumerable<int> questIds)
    {
        kind = WorldInteractableKind.Npc;
        npcId = id;
        displayName = string.IsNullOrWhiteSpace(npcName) ? DisplayName : npcName;
        description = npcDescription ?? string.Empty;
        greetingText = greeting ?? string.Empty;
        interactionRadius = Mathf.Max(0.5f, radius);
        linkedQuestIds = questIds == null ? new int[0] : new List<int>(questIds).ToArray();
    }

    public void ConfigureObject(string key, string objectName, string type, int linkedQuestId, int delta, float radius)
    {
        kind = WorldInteractableKind.Object;
        objectKey = string.IsNullOrWhiteSpace(key) ? gameObject.name : key;
        displayName = string.IsNullOrWhiteSpace(objectName) ? gameObject.name : objectName;
        interactionType = string.IsNullOrWhiteSpace(type) ? "Interact" : type;
        questId = linkedQuestId;
        progressDelta = Mathf.Max(1, delta);
        interactionRadius = Mathf.Max(0.5f, radius);
        description = interactionType;
        greetingText = string.Empty;
        linkedQuestIds = linkedQuestId > 0 ? new[] { linkedQuestId } : new int[0];
    }

    public void ConfigureQuestItem(string key, string itemName, int linkedQuestId, int delta, float radius)
    {
        ConfigureObject(key, itemName, "Collect", linkedQuestId, delta, radius);
        kind = WorldInteractableKind.QuestItem;
    }

    public void ConfigureDungeon(int configId, float radius)
    {
        kind = WorldInteractableKind.Dungeon;
        npcId = configId;
        interactionRadius = Mathf.Max(0.5f, radius);
        displayName = "Dungeon Entrance";
        objectKey = "dungeon_" + configId;
    }

    public string GetPromptText()
    {
        if (kind == WorldInteractableKind.Dungeon)
        {
            if (WorldState.PlayerLevel < 5)
                return "Yêu cầu Cấp 5 để vào Dungeon";
            return "Press E to Enter Dungeon";
        }

        if (kind == WorldInteractableKind.Npc)
            return $"{DisplayName}\nPress E to talk";

        if (objectKey == "dungeon_chest")
            return $"{DisplayName}\nPress E to {InteractionType}";

        if (kind == WorldInteractableKind.QuestItem)
            return $"{DisplayName}\nPress E to collect";

        return $"{DisplayName}\nPress E to {InteractionType}";
    }
}


