using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class MainFeatureUnlockRuntime : MonoBehaviour
{
    public const int InventoryButtonLevel = 2;
    public const int MiniMapButtonLevel = 2;
    public const int ShopButtonLevel = 3;
    public const int GachaButtonLevel = 4;
    public const int SkillButtonLevel = 5;
    public const int GuildButtonLevel = 5;

    private readonly Dictionary<string, GameObject> cachedObjects = new();

    // Performs startup initialization for MainFeatureUnlockRuntime on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        CacheObjects();
        LoadLocalLevel();
        Apply();
        RefreshLevelFromApi();
        WorldRuntimeEvents.LevelChanged += Apply;
        WorldRuntimeEvents.QuestsChanged += RefreshLevelFromApi;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        WorldRuntimeEvents.LevelChanged -= Apply;
        WorldRuntimeEvents.QuestsChanged -= RefreshLevelFromApi;
    }

    // Executes load local level operation.
    private void LoadLocalLevel()
    {
        WorldState.PlayerLevel = Mathf.Max(1, PlayerPrefs.GetInt(ApiConfig.PlayerLevelKey, WorldState.PlayerLevel));
    }

    // Executes refresh level from api operation.
    private void RefreshLevelFromApi()
    {
        if (!ApiClient.Instance.HasToken())
            return;

        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                if (profile == null)
                    return;

                WorldState.PlayerLevel = Mathf.Max(1, profile.Level);
                PlayerPrefs.SetInt(ApiConfig.PlayerLevelKey, WorldState.PlayerLevel);
                PlayerPrefs.Save();
                Apply();
            },
            error => Debug.LogWarning($"[MainFeatureUnlockRuntime] GetMyProfile failed: {error.Message}")
        );
    }

    // Executes apply operation.
    private void Apply()
    {
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        CacheObjects();
        var level = Mathf.Max(1, WorldState.PlayerLevel);

        SetFeatureVisible("InventoryButton", "InventoryPanel", level >= InventoryButtonLevel);
        SetFeatureVisible("MiniMapButton", "MiniMap", level >= MiniMapButtonLevel);

        if (level >= MiniMapButtonLevel && !PlayerUIHotkeys.MinimapVisible)
            SetVisible("MiniMapButton", false);
        SetFeatureVisible("ShopButton", "ShopPanel", level >= ShopButtonLevel);
        SetFeatureVisible("GachaButton", "GachaPanel", level >= GachaButtonLevel);
        SetFeatureVisible("SkillButton", "SkillPanel", level >= SkillButtonLevel);
        SetFeatureVisible("GuildButton", "GuildPanel", level >= GuildButtonLevel);

        var questTracker = FindSceneObject("QuestTracker");
        if (questTracker != null)
            questTracker.SetActive(true);

        ApplyButtonGroupVisibility("BottomRightMenu");
    }

    // Executes apply button group visibility operation.
    private void ApplyButtonGroupVisibility(string groupName)
    {
        var group = FindSceneObject(groupName);
        if (group == null)
            return;

        var buttons = group.GetComponentsInChildren<Button>(true);
        var hasVisibleButton = false;
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button != null && button.gameObject != group && button.gameObject.activeSelf)
            {
                hasVisibleButton = true;
                break;
            }
        }

        group.SetActive(hasVisibleButton);
    }
    // Executes set feature visible operation.
    private void SetFeatureVisible(string buttonName, string panelName, bool visible)
    {
        SetVisible(buttonName, visible);

        if (visible)
            return;

        var panel = FindSceneObject(panelName);
        if (panel == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(panel);
        else
            panel.SetActive(false);
    }

    // Executes set visible operation.
    private void SetVisible(string objectName, bool visible)
    {
        var target = FindSceneObject(objectName);
        if (target != null)
            target.SetActive(visible);
    }

    // Executes cache objects operation.
    private void CacheObjects()
    {
        CacheObject("InventoryButton");
        CacheObject("InventoryPanel");
        CacheObject("MiniMapButton");
        CacheObject("MiniMap");
        CacheObject("ShopButton");
        CacheObject("ShopPanel");
        CacheObject("GachaButton");
        CacheObject("GachaPanel");
        CacheObject("SkillButton");
        CacheObject("SkillPanel");
        CacheObject("GuildButton");
        CacheObject("GuildPanel");
        CacheObject("QuestTracker");
        CacheObject("BottomRightMenu");
    }

    // Executes cache object operation.
    private void CacheObject(string objectName)
    {
        if (cachedObjects.ContainsKey(objectName) && cachedObjects[objectName] != null)
            return;

        var found = FindSceneObjectSlow(objectName);
        if (found != null)
            cachedObjects[objectName] = found;
    }

    // Executes find scene object operation.
    // Validates input parameters against null or empty values.
    private GameObject FindSceneObject(string objectName)
    {
        CacheObject(objectName);
        return cachedObjects.TryGetValue(objectName, out var target) ? target : null;
    }

    // Executes find scene object slow operation.
    // Validates input parameters against null or empty values.
    private static GameObject FindSceneObjectSlow(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in objects)
        {
            if (obj.name == objectName && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
                return obj;
        }

        return null;
    }
}
