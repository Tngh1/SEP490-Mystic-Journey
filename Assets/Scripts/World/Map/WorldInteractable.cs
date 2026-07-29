using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum WorldInteractableKind
{
    Npc,
    Object,
    QuestItem,
    Dungeon
}

public class WorldInteractable : MonoBehaviour
{
    private Canvas overheadCanvas;
    private TextMeshProUGUI overheadText;
    private Coroutine overheadCoroutine;
    // True while ShowInvestigationText owns the overhead label. Not derived from
    // overheadCoroutine: StartCoroutine runs the body up to the first yield BEFORE it
    // returns the handle, so a body that bails out early would be overwritten by a
    // stale non-null handle and block the label forever.
    private bool investigationTextActive;

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

    public WorldInteractableKind Kind => kind;
    public int NpcId => npcId;
    public string DisplayName => (string.IsNullOrWhiteSpace(displayName) || displayName.Equals("Interactable", System.StringComparison.OrdinalIgnoreCase)) ? gameObject.name : displayName;
    public string Description => description;
    public string GreetingText => greetingText;
    public float InteractionRadius => Mathf.Max(0.5f, interactionRadius);
    public string ObjectKey => string.IsNullOrWhiteSpace(objectKey) ? gameObject.name : objectKey;
    public string InteractionType => string.IsNullOrWhiteSpace(interactionType) ? "Interact" : interactionType;
    public int? QuestId => questId > 0 ? questId : null;
    public int ProgressDelta => Mathf.Max(1, progressDelta);
    public IReadOnlyList<int> LinkedQuestIds => linkedQuestIds;
    public Sprite PortraitSprite => portraitSprite;

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

    public void ConfigureQuestItem(string key, string itemName, int linkedQuestId, int delta, float radius)
    {
        // Preserve the interaction type set in the Inspector, default to "Collect" if empty
        string type = string.IsNullOrWhiteSpace(InteractionType) ? "Collect" : InteractionType;
        ConfigureObject(key, itemName, type, linkedQuestId, delta, radius);
        kind = WorldInteractableKind.QuestItem;
        
        UpdateOverheadUI();
    }

    public void ConfigureDungeon(int configId, float radius)
    {
        kind = WorldInteractableKind.Dungeon;
        npcId = configId;
        interactionRadius = Mathf.Max(0.5f, radius);
        displayName = "Dungeon Entrance";
        objectKey = "dungeon_" + configId;
        
        UpdateOverheadUI();
    }

    private void Start()
    {
        CreateOverheadUI();
    }

    private void Update()
    {
        if (overheadCanvas != null)
        {
            // Counteract any flip in the parent hierarchy so text is never backwards
            float sign = transform.lossyScale.x < 0 ? -1f : 1f;
            if (overheadCanvas.transform.localScale.x != sign)
            {
                overheadCanvas.transform.localScale = new Vector3(sign, 1f, 1f);
            }

            // NPC prefabs face left via transform.rotation = Euler(0,-180,0) (EnemyBehaviour.ChangeFaceDir),
            // which mirrors every child - including this canvas. Keep the label world-aligned.
            if (overheadCanvas.transform.rotation != Quaternion.identity)
            {
                overheadCanvas.transform.rotation = Quaternion.identity;
            }
        }
    }

    private void CreateOverheadUI()
    {
        // Recursively find and destroy any existing OverheadUI or legacy text components
        var oldTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in oldTexts)
        {
            // If the text is inside a Canvas that is a child of the NPC, destroy the whole Canvas
            var parentCanvas = t.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.gameObject != this.gameObject)
            {
                Destroy(parentCanvas.gameObject);
            }
            // Otherwise just destroy the text object itself
            else if (t.gameObject != this.gameObject)
            {
                Destroy(t.gameObject);
            }
        }

        var go = new GameObject("OverheadUI");
        go.transform.SetParent(transform, false);
        // Position will be set dynamically in UpdateOverheadUI

        overheadCanvas = go.AddComponent<Canvas>();
        overheadCanvas.renderMode = RenderMode.WorldSpace;
        overheadCanvas.sortingLayerName = "Default";
        overheadCanvas.sortingOrder = 50;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        
        overheadText = textGo.AddComponent<TextMeshProUGUI>();
        overheadText.alignment = TextAlignmentOptions.Center;
        overheadText.enableWordWrapping = false;
        overheadText.overflowMode = TextOverflowModes.Overflow;
        
        // Crisp text with TMP
        overheadText.fontSize = 120;
        
        // Scale down for World Space Canvas
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(1200f, 250f);
        textRect.localScale = new Vector3(0.0025f, 0.0025f, 1f);

        overheadText.outlineWidth = 0.15f;
        overheadText.outlineColor = new Color32(0, 0, 0, 255);

        UpdateOverheadUI();
    }

    public void UpdateOverheadUI()
    {
        if (overheadText == null) return;

        // Interact raises QuestsChanged -> RefreshFromApi -> ConfigureQuestItem -> here.
        // That round-trip can land while ShowInvestigationText is on screen and would
        // overwrite the line with "?" again. The coroutine owns the label until it ends.
        if (investigationTextActive) return;

        // Move text higher for NPCs so it doesn't overlap their sprite
        float heightOffset = (kind == WorldInteractableKind.Npc) ? 2.3f : 1.2f;
        overheadCanvas.transform.localPosition = new Vector3(0, heightOffset, 0);

        if (kind == WorldInteractableKind.Npc)
        {
            overheadText.text = DisplayName;
            overheadText.color = new Color(0.6f, 0.9f, 1f); // Light blue for NPC
            overheadText.fontSize = 100;
            overheadCanvas.gameObject.SetActive(true);
        }
        else if (IsInvestigationItem())
        {
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

    private bool IsInvestigationItem()
    {
        if (kind != WorldInteractableKind.QuestItem) return false;

        // DiggingInteractable luôn hiện "?" (đào đất, khám phá v.v.)
        if (GetComponent<DiggingInteractable>() != null) return true;

        // Các vật phẩm mang tính "điều tra" theo tên: Corpse, Skull, Hộp sọ, Xác...
        return DisplayName.IndexOf("Corpse", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               ObjectKey.IndexOf("Corpse",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               DisplayName.IndexOf("Skull",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               ObjectKey.IndexOf("Skull",   System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private System.Collections.IEnumerator ShowInvestigationText()
    {
        // Bailing out early must also release the label, otherwise UpdateOverheadUI
        // stays blocked and the "?" never comes back.
        if (overheadText == null)
        {
            investigationTextActive = false;
            yield break;
        }

        investigationTextActive = true;

        overheadText.text = "Why is there a corpse? What happened...";
        overheadText.color = Color.white;
        overheadText.fontSize = 80;
        overheadCanvas.gameObject.SetActive(true);

        yield return new WaitForSeconds(3.5f);

        if (overheadCanvas != null)
        {
            overheadCanvas.gameObject.SetActive(false);
        }

        // Release the label back to UpdateOverheadUI (it early-returns while this runs).
        investigationTextActive = false;
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
        {
            string actionStr = string.IsNullOrWhiteSpace(InteractionType) ? "collect" : InteractionType.ToLower();
            return $"{DisplayName}\nPress E to {actionStr}";
        }


        return $"{DisplayName}\nPress E to {InteractionType}";
    }

    public void OnSuccessfulInteraction()
    {
        Debug.Log($"[WorldInteractable] OnSuccessfulInteraction called on {gameObject.name}. Kind: {kind}, InteractionType: '{InteractionType}'");

        if (kind == WorldInteractableKind.QuestItem || kind == WorldInteractableKind.Object)
        {
            // Nếu là cổng/cầu khóa bằng chìa (LockedBridgeGate), ủy quyền kiểm tra cho nó.
            var bridgeGate = GetComponent<LockedBridgeGate>();
            if (bridgeGate != null)
            {
                bridgeGate.InteractWithGate();
                return;
            }

            // Nếu là vật thể "đào" (DiggingInteractable), ủy quyền hoàn toàn cho nó.
            var digInteractable = GetComponent<DiggingInteractable>();
            if (digInteractable != null)
            {
                digInteractable.StartDig();
                return;
            }

            // Nếu là Cây Khởi Nguyên (OriginTreeInteractable), ủy quyền hoàn toàn cho nó.
            var treeInteractable = GetComponent<OriginTreeInteractable>();
            if (treeInteractable != null)
            {
                treeInteractable.StartHeal();
                return;
            }

            // Nếu là Cây Thường Xuân (IvyTreeInteractable), ủy quyền hoàn toàn cho nó.
            var ivyInteractable = GetComponent<IvyTreeInteractable>();
            if (ivyInteractable != null)
            {
                ivyInteractable.StartInteraction();
                return;
            }

            // Vật phẩm "điều tra" (xác, hộp sọ): PHẢI ở lại hiện trường sau khi tương tác.
            // WorldSceneInteractableBootstrap tự gắn WorldRespawnable cho mọi object tag
            // "QuestItem", và WorldRespawnable tắt toàn bộ Renderer 30s -> "xác bị biến mất".
            // Chặn trước nhánh respawner/Collect để xác chỉ tắt collider và hiện thoại.
            if (IsInvestigationItem())
            {
                var invCol = GetComponent<UnityEngine.Collider>();
                if (invCol != null) invCol.enabled = false;
                var invCol2D = GetComponent<UnityEngine.Collider2D>();
                if (invCol2D != null) invCol2D.enabled = false;

                WorldInteractionPromptRuntime.Hide();

                if (overheadCoroutine != null) StopCoroutine(overheadCoroutine);
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
                    
                    var boat = GetComponent<BoatVideoTeleporter>();
                    if (boat != null) boat.InteractWithBoat();

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
