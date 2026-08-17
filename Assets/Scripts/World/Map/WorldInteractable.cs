using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Executes world interactable kind operation.
// Validates input parameters against null or empty values.
// Evaluates conditions and returns a boolean result.
public enum WorldInteractableKind
{
    Npc,
    Object,
    QuestItem,
    Dungeon
}

// Executes mono behaviour operation.
public class WorldInteractable : MonoBehaviour
{
    public static readonly List<WorldInteractable> All = new List<WorldInteractable>();

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        All.Remove(this);
    }

    private Canvas overheadCanvas;
    private TextMeshProUGUI overheadText;
    private Coroutine overheadCoroutine;
    private bool investigationTextActive;

    private bool investigationConsumed;
    // Executes investigation consumed operation.
    public bool InvestigationConsumed => investigationConsumed;

    private static readonly string[] InvestigationLines =
    {
        "A city guard, cut down before he could draw his blade.",
        "Not a mark on this one. Whatever killed him never needed steel.",
        "A mother, curled around her child. Neither of them cried out.",
        "The blood has gone black and cold. No bandit leaves a street like this.",
        "Four deep furrows across the ribs. Nothing human has claws.",
        "He fell running toward the gate, not away from it.",
        "Her lantern is still burning. This happened only hours ago.",
        "None of them have begun to rot. Something here is holding them."
    };
    private static int investigationLineCursor;

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

    [Header("UI")]
    [SerializeField] private Sprite portraitSprite;

    // Executes kind operation.
    // Validates input parameters against null or empty values.
    public WorldInteractableKind Kind => kind;
    // Executes npc id operation.
    // Validates input parameters against null or empty values.
    public int NpcId => npcId;
    // Executes display name operation.
    // Validates input parameters against null or empty values.
    public string DisplayName => (string.IsNullOrWhiteSpace(displayName) || displayName.Equals("Interactable", System.StringComparison.OrdinalIgnoreCase)) ? gameObject.name : displayName;
    // Executes description operation.
    // Validates input parameters against null or empty values.
    public string Description => description;
    // Executes greeting text operation.
    // Validates input parameters against null or empty values.
    public string GreetingText => greetingText;
    // Executes interaction radius operation.
    // Validates input parameters against null or empty values.
    public float InteractionRadius => Mathf.Max(0.5f, interactionRadius);
    // Executes object key operation.
    // Validates input parameters against null or empty values.
    public string ObjectKey => string.IsNullOrWhiteSpace(objectKey) ? gameObject.name : objectKey;
    // Executes interaction type operation.
    // Validates input parameters against null or empty values.
    public string InteractionType => string.IsNullOrWhiteSpace(interactionType) ? "Interact" : interactionType;
    // Executes quest id operation.
    // Validates input parameters against null or empty values.
    public int? QuestId => questId > 0 ? questId : null;
    // Executes progress delta operation.
    // Validates input parameters against null or empty values.
    public int ProgressDelta => Mathf.Max(1, progressDelta);
    // Executes linked quest ids operation.
    // Validates input parameters against null or empty values.
    public IReadOnlyList<int> LinkedQuestIds => linkedQuestIds;
    // Executes portrait sprite operation.
    // Validates input parameters against null or empty values.
    public Sprite PortraitSprite => portraitSprite;

    // Executes configure npc operation.
    // Validates input parameters against null or empty values.
    public void ConfigureNpc(int id, string npcName, string npcDescription, string greeting, float radius, IEnumerable<int> questIds)
    {
        kind = WorldInteractableKind.Npc;
        npcId = id;
        displayName = string.IsNullOrWhiteSpace(npcName) ? DisplayName : npcName;
        description = npcDescription ?? string.Empty;
        greetingText = greeting ?? string.Empty;
        interactionRadius = Mathf.Max(0.5f, radius);
        linkedQuestIds = questIds == null ? new int[0] : new List<int>(questIds).ToArray();

        UpdateOverheadUI();
    }

    // Executes configure object operation.
    // Validates input parameters against null or empty values.
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

        UpdateOverheadUI();
    }

    // Executes configure quest item operation.
    // Validates input parameters against null or empty values.
    public void ConfigureQuestItem(string key, string itemName, int linkedQuestId, int delta, float radius)
    {
        string type = string.IsNullOrWhiteSpace(InteractionType) ? "Collect" : InteractionType;
        ConfigureObject(key, itemName, type, linkedQuestId, delta, radius);
        kind = WorldInteractableKind.QuestItem;

        UpdateOverheadUI();
    }

    // Executes configure dungeon operation.
    public void ConfigureDungeon(int configId, float radius)
    {
        kind = WorldInteractableKind.Dungeon;
        npcId = configId;
        interactionRadius = Mathf.Max(0.5f, radius);
        displayName = "Dungeon Entrance";
        objectKey = "dungeon_" + configId;

        UpdateOverheadUI();
    }

    // Performs startup initialization for WorldInteractable on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        CreateOverheadUI();
    }

    // Per-frame update loop for WorldInteractable.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (overheadCanvas != null)
        {
            float sign = transform.lossyScale.x < 0 ? -1f : 1f;
            if (overheadCanvas.transform.localScale.x != sign)
            {
                overheadCanvas.transform.localScale = new Vector3(sign, 1f, 1f);
            }

            if (overheadCanvas.transform.rotation != Quaternion.identity)
            {
                overheadCanvas.transform.rotation = Quaternion.identity;
            }
        }
    }

    // Executes create overhead ui operation.
    private void CreateOverheadUI()
    {
        var oldTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in oldTexts)
        {
            var parentCanvas = t.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.gameObject != this.gameObject)
            {
                Destroy(parentCanvas.gameObject);
            }
            else if (t.gameObject != this.gameObject)
            {
                Destroy(t.gameObject);
            }
        }

        var go = new GameObject("OverheadUI");
        go.transform.SetParent(transform, false);

        overheadCanvas = go.AddComponent<Canvas>();
        overheadCanvas.renderMode = RenderMode.WorldSpace;
        overheadCanvas.sortingLayerName = "Default";
        overheadCanvas.sortingOrder = 50;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);

        overheadText = textGo.AddComponent<TextMeshProUGUI>();
        overheadText.alignment = TextAlignmentOptions.Center;
        overheadText.textWrappingMode = TextWrappingModes.NoWrap;
        overheadText.overflowMode = TextOverflowModes.Overflow;

        overheadText.fontSize = 120;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(1200f, 250f);
        textRect.localScale = new Vector3(0.0025f, 0.0025f, 1f);

        overheadText.outlineWidth = 0.15f;
        overheadText.outlineColor = new Color32(0, 0, 0, 255);

        UpdateOverheadUI();
    }

    // Executes update overhead ui operation.
    public void UpdateOverheadUI()
    {
        if (overheadText == null) return;

        if (investigationTextActive) return;

        float heightOffset = (kind == WorldInteractableKind.Npc) ? 2.3f : 1.2f;
        overheadCanvas.transform.localPosition = new Vector3(0, heightOffset, 0);

        if (kind == WorldInteractableKind.Npc)
        {
            overheadText.text = DisplayName;
            overheadText.color = new Color(0.6f, 0.9f, 1f);
            overheadText.fontSize = 100;
            overheadCanvas.gameObject.SetActive(true);
        }
        else if (IsInvestigationItem())
        {
            if (investigationConsumed)
            {
                overheadCanvas.gameObject.SetActive(false);
                return;
            }

            overheadText.text = "?";
            overheadText.color = Color.yellow;
            overheadText.fontSize = 160;
            overheadCanvas.gameObject.SetActive(true);
        }
        else
        {
            overheadCanvas.gameObject.SetActive(false);
        }
    }

    // Executes is investigation item operation.
    // Evaluates conditions and returns a boolean result.
    private bool IsInvestigationItem()
    {
        if (kind != WorldInteractableKind.QuestItem) return false;

        if (GetComponent<DiggingInteractable>() != null) return true;

        return DisplayName.IndexOf("Corpse", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               ObjectKey.IndexOf("Corpse",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               DisplayName.IndexOf("Skull",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               ObjectKey.IndexOf("Skull",   System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Executes show investigation text operation.
    private System.Collections.IEnumerator ShowInvestigationText()
    {
        if (overheadText == null)
        {
            investigationTextActive = false;
            yield break;
        }

        investigationTextActive = true;

        overheadText.text = InvestigationLines[investigationLineCursor % InvestigationLines.Length];
        investigationLineCursor++;
        overheadText.color = Color.white;
        overheadText.fontSize = 80;
        overheadCanvas.gameObject.SetActive(true);

        yield return new WaitForSeconds(3.5f);

        if (overheadCanvas != null)
        {
            overheadCanvas.gameObject.SetActive(false);
        }

        investigationTextActive = false;
    }

    // Executes get prompt text operation.
    public string GetPromptText()
    {
        if (kind == WorldInteractableKind.Dungeon)
        {
            var entrance = GetComponent<DungeonEntrance>();

            if (entrance == null || !entrance.RequiredLevel.HasValue)
                return "Checking dungeon requirements...";

            int required = entrance.RequiredLevel.Value;
            if (WorldState.PlayerLevel < required)
                return $"Requires Level {required} to enter the Dungeon";
            return "Press E to Enter Dungeon";
        }

        if (kind == WorldInteractableKind.Npc)
            return $"{DisplayName}\nPress E to talk";

        if (objectKey == "dungeon_chest")
            return $"{DisplayName}\nPress E to {InteractionType}";

        if (kind == WorldInteractableKind.QuestItem)
        {
            string actionStr = string.IsNullOrWhiteSpace(InteractionType) ? "collect" : InteractionType.ToLower();
            return $"{DisplayName}\nPress E to {actionStr}";
        }


        return $"{DisplayName}\nPress E to {InteractionType}";
    }

    // Executes on successful interaction operation.
    public void OnSuccessfulInteraction()
    {
        Debug.Log($"[WorldInteractable] OnSuccessfulInteraction called on {gameObject.name}. Kind: {kind}, InteractionType: '{InteractionType}'");

        if (kind == WorldInteractableKind.QuestItem || kind == WorldInteractableKind.Object)
        {
            var bridgeGate = GetComponent<LockedBridgeGate>();
            if (bridgeGate != null)
            {
                bridgeGate.InteractWithGate();
                return;
            }

            var digInteractable = GetComponent<DiggingInteractable>();
            if (digInteractable != null)
            {
                digInteractable.StartDig();
                return;
            }

            var treeInteractable = GetComponent<OriginTreeInteractable>();
            if (treeInteractable != null)
            {
                treeInteractable.StartHeal();
                return;
            }

            var ivyInteractable = GetComponent<IvyTreeInteractable>();
            if (ivyInteractable != null)
            {
                ivyInteractable.StartInteraction();
                return;
            }

            var boatTeleporter = GetComponent<BoatVideoTeleporter>();
            if (boatTeleporter != null)
            {
                boatTeleporter.InteractWithBoat();
                WorldInteractionPromptRuntime.Hide();
                return;
            }

            if (IsInvestigationItem())
            {
                investigationConsumed = true;

                var invCol = GetComponent<UnityEngine.Collider>();
                if (invCol != null) invCol.enabled = false;
                var invCol2D = GetComponent<UnityEngine.Collider2D>();
                if (invCol2D != null) invCol2D.enabled = false;

                WorldInteractionPromptRuntime.Hide();

                if (overheadCoroutine != null) StopCoroutine(overheadCoroutine);
                // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                overheadCoroutine = StartCoroutine(ShowInvestigationText());
                return;
            }

            var respawner = GetComponent<WorldRespawnable>();

            if (respawner != null)
            {
                Debug.Log($"[WorldInteractable] Calling ConsumeAndRespawn on {gameObject.name}");
                respawner.ConsumeAndRespawn();
                WorldInteractionPromptRuntime.Hide();
            }
            else
            {
                bool isCollectOrGather = InteractionType.Equals("Collect", System.StringComparison.OrdinalIgnoreCase) ||
                                         InteractionType.Equals("Gather", System.StringComparison.OrdinalIgnoreCase);

                if (isCollectOrGather)
                {
                    Debug.Log($"[WorldInteractable] Is Collect/Gather but no respawner. Hiding object {gameObject.name}");
                    gameObject.SetActive(false);
                    WorldInteractionPromptRuntime.Hide();
                }
                else
                {
                    Debug.Log($"[WorldInteractable] Not Collect/Gather. Disabling colliders on {gameObject.name} instead of hiding.");

                    var col = GetComponent<UnityEngine.Collider>();
                    if (col != null) col.enabled = false;
                    var col2D = GetComponent<UnityEngine.Collider2D>();
                    if (col2D != null) col2D.enabled = false;

                    WorldInteractionPromptRuntime.Hide();
                }
            }
        }
    }


}
