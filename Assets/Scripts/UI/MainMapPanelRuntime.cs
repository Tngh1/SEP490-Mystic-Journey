using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    
    [Header("Progress")]
    public TMP_Text explorationText; // e.g. "Exploration: 100%"
    public Image progressBarFill; // ProgressBar fill amount
}

public class MainMapPanelRuntime : MonoBehaviour
{
    [Header("UI Elements")]
    public RawImage mapBackground;
    public Button continueButton;

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

    private readonly Dictionary<string, bool> _mapUnlockState =
        new(StringComparer.OrdinalIgnoreCase);

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
    }

    private void OnEnable()
    {
        SyncMinimapBackground();

        // The panel shows the whole level, not the player's surroundings, so the
        // shared minimap camera zooms out to frame everything while it is open.
        if (MinimapCameraController.Instance != null)
            MinimapCameraController.Instance.ShowFullMap();

        WorldRuntimeEvents.MapCompleted += OnQuestClaimedCheckMapUnlock;
        WorldRuntimeEvents.QuestsChanged += OnQuestsChanged;
        WorldRuntimeEvents.MapChanged += OnMapChanged;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsLoaded -= FetchMapProgress;
            QuestManager.Instance.OnQuestsLoaded += FetchMapProgress;
            QuestManager.Instance.LoadMyQuests();
        }

        FetchMapProgress();
    }

    private void OnDisable()
    {
        // Hand the camera back to the HUD minimap, otherwise it stays zoomed out.
        if (MinimapCameraController.Instance != null)
            MinimapCameraController.Instance.ShowMinimap();

        WorldRuntimeEvents.MapCompleted -= OnQuestClaimedCheckMapUnlock;
        WorldRuntimeEvents.QuestsChanged -= OnQuestsChanged;
        WorldRuntimeEvents.MapChanged -= OnMapChanged;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsLoaded -= FetchMapProgress;
        }
    }

    private void BindMapButton(UIMapSlotReference slot)
    {
        if (slot.clickButton != null && slot.mapData != null)
        {
            slot.clickButton.onClick.RemoveAllListeners();
            slot.clickButton.onClick.AddListener(() => OnMapButtonClicked(slot));
        }
    }

    private void FetchMapProgress()
    {
        if (isFetchingData)
        {
            pendingFetch = true;
            return;
        }

        isFetchingData = true;

        WorldApi.Instance.GetState(
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
                Debug.LogError($"[MainMapPanelRuntime] Failed to fetch World State: {error.Message}");
                CompleteFetch();
            }
        );
    }

    private void CompleteFetch()
    {
        isFetchingData = false;

        if (!pendingFetch) return;
        pendingFetch = false;
        FetchMapProgress();
    }

    private void UpdateSlotsUI(List<WorldMapProgressResponse> mapsProgress)
    {
        foreach (var slot in allSlots)
        {
            if (slot == null || slot.mapData == null) continue;

            var progress = mapsProgress?.FirstOrDefault(m =>
                string.Equals(m.MapName, slot.mapData.mapName, StringComparison.OrdinalIgnoreCase));

            var apiUnlocked = progress != null && progress.IsUnlocked;
            var questUnlocked = QuestManager.Instance != null && QuestManager.Instance.CanEnterMap(slot.mapData);
            var isUnlocked = slot.mapData.unlockQuestId <= 0 || apiUnlocked || questUnlocked;

            int explorationPct = progress?.ExplorationPercent ?? 0;
            string displayName = !string.IsNullOrEmpty(progress?.DisplayName)
                ? progress.DisplayName
                : slot.mapData.mapName;

            _mapUnlockState[slot.mapData.mapName] = isUnlocked;

            if (slot.mapNameText != null)
                slot.mapNameText.text = displayName;

            if (slot.unlockedGroup != null)
                slot.unlockedGroup.SetActive(isUnlocked);

            if (slot.lockedGroup != null)
                slot.lockedGroup.SetActive(!isUnlocked);

            if (slot.explorationText != null)
                slot.explorationText.text = $"Exploration: {explorationPct}%";

            if (slot.progressBarFill != null)
                slot.progressBarFill.fillAmount = explorationPct / 100f;

            if (slot.mapThumbnail != null && slot.mapData.thumbnail != null)
                slot.mapThumbnail.sprite = slot.mapData.thumbnail;
        }
    }

    private void OnMapButtonClicked(UIMapSlotReference slot)
    {
        if (slot.mapData == null) return;

        _mapUnlockState.TryGetValue(slot.mapData.mapName, out bool canEnter);

        if (canEnter)
        {
            if (mapDetailPopup != null)
            {
                mapDetailPopup.gameObject.SetActive(true);
                mapDetailPopup.Setup(slot.mapData);
            }
            else
            {
                Debug.LogWarning("[MainMapPanelRuntime] Map Detail Popup is not assigned!");
            }
        }
        else
        {
            Debug.Log($"[MainMapPanelRuntime] Map '{slot.mapData.mapName}' is locked.");
        }
    }

    private void SyncMinimapBackground()
    {
        if (mapBackground == null) return;

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

    private void OnQuestClaimedCheckMapUnlock(int claimedQuestId)
    {
        if (allSlots == null) return;

        bool newMapJustUnlocked = allSlots.Any(slot =>
            slot != null &&
            slot.mapData != null &&
            slot.mapData.unlockQuestId == claimedQuestId);

        if (!newMapJustUnlocked) return;

        Debug.Log($"[MainMapPanelRuntime] Map unlocked by questId={claimedQuestId}. Opening Map Panel.");
        StartCoroutine(OpenMapPanelDelayed());
    }

    private void OnQuestsChanged()
    {
        FetchMapProgress();
    }

    private void OnMapChanged(string mapName)
    {
        SyncMinimapBackground();
        FetchMapProgress();
    }

    private IEnumerator OpenMapPanelDelayed()
    {
        yield return new WaitForSeconds(1.5f);

        FetchMapProgress();

        if (UIManager.Instance != null && UIManager.Instance.mapPanel != null)
        {
            UIManager.Instance.ShowPanel(UIManager.Instance.mapPanel);
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    private void ClosePanel()
    {
        gameObject.SetActive(false);
    }

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
