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
    public TMP_Text explorationText; // chỉ phần số, label "Exploration:" là text tĩnh của parent
    public Image progressBarFill; // ProgressBar fill amount
}

public class MainMapPanelRuntime : MonoBehaviour
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
        // Scene lưu ActiveBorder ở trạng thái bật để dễ chỉnh giao diện, nên phải
        // dựng lại trạng thái đúng ngay khi mở panel, trước khi API trả về.
        ApplyLocalStateBeforeFetch();

        // The panel shows the whole level, not the player's surroundings, so the
        // shared minimap camera zooms out to frame everything while it is open.
        if (MinimapCameraController.Instance != null)
            MinimapCameraController.Instance.ShowFullMap();

        // Must run after ShowFullMap: that call swaps the camera onto the wide
        // full-map texture, and the RawImage has to follow it.
        SyncMinimapBackground();

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

    // Dựng trạng thái từ dữ liệu có sẵn ở client (quest + map đang đứng) để panel
    // không nháy sai một nhịp trong lúc chờ WorldApi, và vẫn đúng nếu API lỗi.
    private void ApplyLocalStateBeforeFetch()
    {
        if (allSlots == null) return;

        string currentMap = WorldState.CurrentMapName;

        foreach (var slot in allSlots)
        {
            if (slot == null || slot.mapData == null) continue;

            bool isUnlocked = slot.mapData.unlockQuestId <= 0 ||
                              (QuestManager.Instance != null &&
                               QuestManager.Instance.CanEnterMap(slot.mapData));

            _mapUnlockState[slot.mapData.mapName] = isUnlocked;

            ApplySlotState(slot, isUnlocked, currentMap);

            if (slot.mapNameText != null)
                slot.mapNameText.text = slot.mapData.mapName;

            if (slot.mapThumbnail != null && slot.mapData.thumbnail != null)
                slot.mapThumbnail.sprite = slot.mapData.thumbnail;
        }
    }

    private static void ApplySlotState(UIMapSlotReference slot, bool isUnlocked, string currentMap)
    {
        if (slot.unlockedGroup != null)
            slot.unlockedGroup.SetActive(isUnlocked);

        if (slot.lockedGroup != null)
            slot.lockedGroup.SetActive(!isUnlocked);

        // ActiveBorder chỉ sáng ở map đang đứng, và chỉ khi map đó đã mở.
        if (slot.activeBorder != null)
            slot.activeBorder.SetActive(isUnlocked && QuestUtils.IsSameMap(currentMap, slot.mapData.mapName));
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

        var api = WorldApi.Instance;
        if (api == null)
        {
            Debug.LogWarning("[MainMapPanelRuntime] WorldApi is unavailable, skipping map progress fetch.");
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
        string currentMap = WorldState.CurrentMapName;
        int pctSum = 0, pctCount = 0;

        foreach (var slot in allSlots)
        {
            if (slot == null || slot.mapData == null) continue;

            // BE trả tên scene liền ("ElfForest"), MapData.mapName có dấu cách ("Elf Forest"),
            // nên so khớp phải bỏ dấu cách chứ không dùng Equals thô.
            var progress = mapsProgress?.FirstOrDefault(m =>
                QuestUtils.IsSameMap(m.MapName, slot.mapData.mapName));

            var apiUnlocked = progress != null && progress.IsUnlocked;
            var questUnlocked = QuestManager.Instance != null && QuestManager.Instance.CanEnterMap(slot.mapData);
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

    private void OnMapButtonClicked(UIMapSlotReference slot)
    {
        if (slot.mapData == null) return;

        // Chặn ngay từ slot: guard thật nằm ở MapSceneController.EnterMap, nhưng nếu chỉ chặn
        // ở đó thì người chơi phải mở popup Detail rồi bấm Enter mới biết là không đi được.
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
        {
            MapSceneController.NotifyTravelBlocked();
            return;
        }

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

        // Prefer the texture the minimap camera is rendering into right now: in
        // full-map mode that is the wide panel texture, whose aspect matches this
        // frame. Reading it from the HUD minimap instead would pin us to the square
        // minimap texture and letterbox the level.
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
        // New level, new bounds: re-frame before rebinding the texture.
        if (MinimapCameraController.Instance != null)
            MinimapCameraController.Instance.ShowFullMap();

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
