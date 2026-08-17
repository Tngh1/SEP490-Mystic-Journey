using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Initializes a new default instance of the UIMapSlotReference class.
[Serializable]
public class UIMapSlotReference
{
    [Tooltip("The MapData for this map")]
    public MapData mapData;

    [Header("UI Elements")]
    public Button clickButton;
    public TMP_Text mapNameText;
    public Image mapThumbnail;

    [Header("State Groups")]
    public GameObject unlockedGroup;
    public GameObject lockedGroup;

    [Tooltip("ActiveMap border, chỉ bật cho map người chơi đang đứng")]
    public GameObject activeBorder;

    [Header("Progress")]
    public TMP_Text explorationText;
    public Image progressBarFill;
}

// Executes mono behaviour operation.
public class MapUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public RawImage mapBackground;
    public Button continueButton;

    [Header("Bottom Bar")]
    [Tooltip("Chỉ phần số của 'All Map Progress:'")]
    public TMP_Text totalProgressText;
    public Image totalProgressBarFill;

    [Header("Map References")]
    public UIMapSlotReference elfForestSlot;
    public UIMapSlotReference autumnPumpkinSlot;
    public UIMapSlotReference frozenMountainsSlot;
    public UIMapSlotReference vestigeOfEraSlot;

    [Header("Popup Reference")]
    [Tooltip("The Map Popup that contains UIMapDetailPanel, located in PopupLayer")]
    public UIMapDetailPanel mapDetailPopup;

    private List<UIMapSlotReference> allSlots;
    private bool isFetchingData;
    private bool pendingFetch;

    private bool _openRejected;

    private readonly Dictionary<string, bool> _mapUnlockState =
        new(StringComparer.OrdinalIgnoreCase);

    // Initializes internal component caches and dependencies for UIMapSlotReference upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        allSlots = new List<UIMapSlotReference>
        {
            elfForestSlot,
            autumnPumpkinSlot,
            frozenMountainsSlot,
            vestigeOfEraSlot
        };
    }

    // Performs startup initialization for UIMapSlotReference on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ClosePanel);
        }

        foreach (var slot in allSlots)
        {
            BindMapButton(slot);
        }

        SetupHoverEffects();
    }

    // Executes setup hover effects operation.
    private void SetupHoverEffects()
    {
        AddHoverEffects(transform);

        if (mapDetailPopup != null)
            AddHoverEffects(mapDetailPopup.transform);
    }

    // Executes add hover effects operation.
    private static void AddHoverEffects(Transform root)
    {
        if (root == null) return;

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (button == null) continue;
            if (button.GetComponent<UIHoverScaleEffect>() == null)
                button.gameObject.AddComponent<UIHoverScaleEffect>();
        }
    }

    // Executes can open operation.
    public static bool CanOpen
    {
        get { return !MapSceneController.IsTravelBlockedNow; }
    }

    // Validates travel permission, switches camera to full map projection, and triggers progress sync.
    private void OnEnable()
    {
        if (!CanOpen)
        {
            Debug.Log("[MapUIManager] Blocked: cannot open the Map Panel inside a dungeon.");
            _openRejected = true;
            gameObject.SetActive(false); // Prohibit map panel while inside dungeon instanced gameplay
            return;
        }

        _openRejected = false;

        ApplyLocalStateBeforeFetch(); // Apply cached quest unlock state and thumbnail art

        if (MinimapCameraController.Instance != null)
            MinimapCameraController.Instance.ShowFullMap(); // Switch minimap camera viewport to full world map

        SyncMinimapBackground();

        WorldRuntimeEvents.MapCompleted += OnQuestClaimedCheckMapUnlock; // Subscribe zone unlock events
        WorldRuntimeEvents.QuestsChanged += OnQuestsChanged; // Subscribe quest progression
        WorldRuntimeEvents.MapChanged += OnMapChanged; // Subscribe zone transition

        if (QuestUIManager.Instance != null)
        {
            QuestUIManager.Instance.OnQuestsLoaded -= FetchMapProgress;
            QuestUIManager.Instance.OnQuestsLoaded += FetchMapProgress;
            QuestUIManager.Instance.LoadMyQuests(); // Refresh active quest requirements
        }

        FetchMapProgress(); // Calculate exploration percentages from discovered tiles/monsters
    }

    // Unsubscribes events and reverts camera to corner minimap view.
    private void OnDisable()
    {
        if (_openRejected)
        {
            _openRejected = false;
            return;
        }

        if (MinimapCameraController.Instance != null)
            MinimapCameraController.Instance.ShowMinimap(); // Revert minimap camera to follow player avatar

        WorldRuntimeEvents.MapCompleted -= OnQuestClaimedCheckMapUnlock;
        WorldRuntimeEvents.QuestsChanged -= OnQuestsChanged;
        WorldRuntimeEvents.MapChanged -= OnMapChanged;

        if (QuestUIManager.Instance != null)
        {
            QuestUIManager.Instance.OnQuestsLoaded -= FetchMapProgress;
        }
    }

    // Initializes map slot UI elements using local WorldState before network fetch completes.
    private void ApplyLocalStateBeforeFetch()
    {
        if (allSlots == null) return;

        string currentMap = WorldState.CurrentMapName; // Get active zone name

        foreach (var slot in allSlots)
        {
            if (slot == null || slot.mapData == null) continue;

            bool isUnlocked = slot.mapData.unlockQuestId <= 0 ||
                              (QuestUIManager.Instance != null &&
                               QuestUIManager.Instance.CanEnterMap(slot.mapData)); // Evaluate prerequisite quest completion

            _mapUnlockState[slot.mapData.mapName] = isUnlocked; // Cache zone lock status

            ApplySlotState(slot, isUnlocked, currentMap); // Update lock overlay and active border

            if (slot.mapNameText != null)
                slot.mapNameText.text = slot.mapData.mapName; // Set zone label

            if (slot.mapThumbnail != null && slot.mapData.thumbnail != null)
                slot.mapThumbnail.sprite = slot.mapData.thumbnail; // Set zone preview art
        }
    }

    // Executes apply slot state operation.
    private static void ApplySlotState(UIMapSlotReference slot, bool isUnlocked, string currentMap)
    {
        if (slot.unlockedGroup != null)
            slot.unlockedGroup.SetActive(isUnlocked);

        if (slot.lockedGroup != null)
            slot.lockedGroup.SetActive(!isUnlocked);

        if (slot.activeBorder != null)
            slot.activeBorder.SetActive(isUnlocked && QuestUtils.IsSameMap(currentMap, slot.mapData.mapName));
    }

    // Executes bind map button operation.
    private void BindMapButton(UIMapSlotReference slot)
    {
        if (slot.clickButton != null && slot.mapData != null)
        {
            slot.clickButton.onClick.RemoveAllListeners();
            slot.clickButton.onClick.AddListener(() => OnMapButtonClicked(slot));
        }
    }

    // Executes fetch map progress operation.
    private void FetchMapProgress()
    {
        if (isFetchingData)
        {
            pendingFetch = true;
            return;
        }

        var api = WorldApi.Instance;
        if (api == null)
        {
            Debug.LogWarning("[MapUIManager] WorldApi is unavailable, skipping map progress fetch.");
            return;
        }

        isFetchingData = true;

        api.GetState(
            state =>
            {
                if (state != null && state.Maps != null)
                {
                    UpdateSlotsUI(state.Maps);
                }

                CompleteFetch();
            },
            error =>
            {
                Debug.LogError($"[MapUIManager] Failed to fetch World State: {error.Message}");
                CompleteFetch();
            }
        );
    }

    // Executes complete fetch operation.
    // Validates input parameters against null or empty values.
    private void CompleteFetch()
    {
        isFetchingData = false;

        if (!pendingFetch) return;
        pendingFetch = false;
        FetchMapProgress();
    }

    // Executes update slots ui operation.
    // Validates input parameters against null or empty values.
    private void UpdateSlotsUI(List<WorldMapProgressResponse> mapsProgress)
    {
        string currentMap = WorldState.CurrentMapName;
        int pctSum = 0, pctCount = 0;

        foreach (var slot in allSlots)
        {
            if (slot == null || slot.mapData == null) continue;

            var progress = mapsProgress?.FirstOrDefault(m =>
                QuestUtils.IsSameMap(m.MapName, slot.mapData.mapName));

            var apiUnlocked = progress != null && progress.IsUnlocked;
            var questUnlocked = QuestUIManager.Instance != null && QuestUIManager.Instance.CanEnterMap(slot.mapData);
            var isUnlocked = slot.mapData.unlockQuestId <= 0 || apiUnlocked || questUnlocked;

            int explorationPct = progress?.ExplorationPercent ?? 0;
            string displayName = !string.IsNullOrEmpty(progress?.DisplayName)
                ? progress.DisplayName
                : slot.mapData.mapName;

            _mapUnlockState[slot.mapData.mapName] = isUnlocked;
            pctSum += explorationPct;
            pctCount++;

            if (slot.mapNameText != null)
                slot.mapNameText.text = displayName;

            ApplySlotState(slot, isUnlocked, currentMap);

            if (slot.explorationText != null)
                slot.explorationText.text = $"{explorationPct}%";

            if (slot.progressBarFill != null)
                slot.progressBarFill.fillAmount = explorationPct / 100f;

            if (slot.mapThumbnail != null && slot.mapData.thumbnail != null)
                slot.mapThumbnail.sprite = slot.mapData.thumbnail;
        }

        int totalPct = pctCount == 0 ? 0 : Mathf.RoundToInt((float)pctSum / pctCount);

        if (totalProgressText != null)
            totalProgressText.text = $"{totalPct}%";

        if (totalProgressBarFill != null)
            totalProgressBarFill.fillAmount = totalPct / 100f;
    }

    // Executes on map button clicked operation.
    private void OnMapButtonClicked(UIMapSlotReference slot)
    {
        if (slot.mapData == null) return;

        if (!CanOpen) return;

        _mapUnlockState.TryGetValue(slot.mapData.mapName, out bool canEnter);

        if (canEnter)
        {
            if (mapDetailPopup != null)
            {
                EnsureAncestorsActive(mapDetailPopup.transform);

                mapDetailPopup.gameObject.SetActive(true);
                mapDetailPopup.Setup(slot.mapData);

                mapDetailPopup.transform.SetAsLastSibling();
            }
            else
            {
                Debug.LogWarning("[MapUIManager] Map Detail Popup is not assigned!");
            }
        }
        else
        {
            Debug.Log($"[MapUIManager] Map '{slot.mapData.mapName}' is locked.");
        }
    }

    // Executes ensure ancestors active operation.
    private static void EnsureAncestorsActive(Transform target)
    {
        if (target == null) return;

        var parents = new Stack<Transform>();
        for (var current = target.parent; current != null; current = current.parent)
        {
            parents.Push(current);
            if (current.GetComponent<Canvas>() != null) break;
        }

        while (parents.Count > 0)
        {
            var parent = parents.Pop();
            if (parent != null && !parent.gameObject.activeSelf)
            {
                Debug.Log($"[MapUIManager] Re-enabling inactive popup ancestor '{parent.name}'.");
                parent.gameObject.SetActive(true);
            }
        }
    }

    // Executes sync minimap background operation.
    private void SyncMinimapBackground()
    {
        if (mapBackground == null) return;

        var minimap = MinimapCameraController.Instance;
        if (minimap != null && minimap.ActiveTexture != null)
        {
            mapBackground.texture = minimap.ActiveTexture;
            return;
        }

        var miniMapObj = FindSceneObject("MiniMap");
        if (miniMapObj != null)
        {
            var miniMapRaw = miniMapObj.GetComponentInChildren<RawImage>(true);
            if (miniMapRaw != null)
            {
                mapBackground.texture = miniMapRaw.texture;
            }
        }
    }

    // Executes on quest claimed check map unlock operation.
    private void OnQuestClaimedCheckMapUnlock(int claimedQuestId)
    {
        if (allSlots == null) return;

        bool newMapJustUnlocked = allSlots.Any(slot =>
            slot != null &&
            slot.mapData != null &&
            slot.mapData.unlockQuestId == claimedQuestId);

        if (!newMapJustUnlocked) return;

        Debug.Log($"[MapUIManager] Map unlocked by questId={claimedQuestId}. Opening Map Panel.");
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(OpenMapPanelDelayed());
    }

    // Executes on quests changed operation.
    private void OnQuestsChanged()
    {
        FetchMapProgress();
    }

    // Executes on map changed operation.
    private void OnMapChanged(string mapName)
    {
        if (!CanOpen)
        {
            ClosePanel();
            return;
        }

        if (MinimapCameraController.Instance != null)
            MinimapCameraController.Instance.ShowFullMap();

        SyncMinimapBackground();
        FetchMapProgress();
    }

    // Executes open map panel delayed operation.
    private IEnumerator OpenMapPanelDelayed()
    {
        yield return new WaitForSeconds(1.5f);

        FetchMapProgress();

        if (!CanOpen) yield break;

        if (UIManager.Instance != null && UIManager.Instance.mapPanel != null)
        {
            UIManager.Instance.ShowPanel(UIManager.Instance.mapPanel);
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    // Update visibility for panel; it updates active.
    private void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    // Executes find scene object operation.
    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in objects)
        {
            if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave)
                continue;

            if (obj.scene.IsValid() && obj.name == objectName)
            {
                return obj;
            }
        }
        return null;
    }
}
