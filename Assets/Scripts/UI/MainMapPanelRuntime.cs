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
    private bool isFetchingData = false;

    // Single source of truth: IsUnlocked đến từ API (WorldApi.GetState).
    // Được populate sau mỗi lần FetchMapProgress thành công.
    private readonly Dictionary<string, bool> _mapUnlockState =
        new(System.StringComparer.OrdinalIgnoreCase);

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
        FetchMapProgress();
        WorldRuntimeEvents.MapCompleted += OnQuestClaimedCheckMapUnlock;
    }

    private void OnDisable()
    {
        WorldRuntimeEvents.MapCompleted -= OnQuestClaimedCheckMapUnlock;
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
        if (isFetchingData) return;
        isFetchingData = true;

        WorldApi.Instance.GetState(
            state =>
            {
                isFetchingData = false;
                if (state != null && state.Maps != null)
                {
                    UpdateSlotsUI(state.Maps);
                }
            },
            error =>
            {
                isFetchingData = false;
                Debug.LogError($"[MainMapPanelRuntime] Failed to fetch World State: {error.Message}");
            }
        );
    }

    private void UpdateSlotsUI(List<WorldMapProgressResponse> mapsProgress)
    {
        foreach (var slot in allSlots)
        {
            if (slot == null || slot.mapData == null) continue;

            // Lấy ExplorationPercent và DisplayName từ API response
            var progress = mapsProgress.FirstOrDefault(m => m.MapName == slot.mapData.mapName);

            // IsUnlocked: dùng MapData.unlockQuestId (Unity asset) + QuestManager (quest state từ server)
            // MapData là nguồn truth về "map này cần unlock quest nào"
            // QuestManager là nguồn truth về "quest đó đã Claimed chưa"
            bool isUnlocked = QuestManager.Instance != null
                && QuestManager.Instance.CanEnterMap(slot.mapData);

            int explorationPct = progress?.ExplorationPercent ?? 0;
            string displayName = (!string.IsNullOrEmpty(progress?.DisplayName))
                ? progress.DisplayName
                : slot.mapData.mapName;

            // Cache lại để OnMapButtonClicked dùng — 1 nguồn truth duy nhất
            _mapUnlockState[slot.mapData.mapName] = isUnlocked;

            // Update UI
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

        // Dùng IsUnlocked từ API (đã cache trong _mapUnlockState) — single source of truth
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

    // Được gọi mỗi khi 1 quest bất kỳ được Claimed.
    // Kiểm tra: quest đó có phải là unlockQuestId của slot nào không?
    // Nếu có → map mới vừa mở → tự động show MapPanel để player chuyển map.
    private void OnQuestClaimedCheckMapUnlock(int claimedQuestId)
    {
        if (allSlots == null) return;

        bool newMapJustUnlocked = allSlots.Any(slot =>
            slot != null &&
            slot.mapData != null &&
            slot.mapData.unlockQuestId == claimedQuestId);

        if (!newMapJustUnlocked) return;

        Debug.Log($"[MainMapPanelRuntime] Map mới unlock (unlockQuestId={claimedQuestId}). Mở Map Panel.");

        // Delay nhỏ để quest popup kịp hiển thị trước khi mở panel
        StartCoroutine(OpenMapPanelDelayed());
    }

    private IEnumerator OpenMapPanelDelayed()
    {
        yield return new WaitForSeconds(1.5f);

        // Refresh data trước khi show để slot hiện đúng trạng thái mới
        FetchMapProgress();

        if (UIManager.Instance != null && UIManager.Instance.mapPanel != null)
        {
            UIManager.Instance.ShowPanel(UIManager.Instance.mapPanel);
        }
        else
        {
            // Fallback: tự SetActive nếu không có UIManager
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
