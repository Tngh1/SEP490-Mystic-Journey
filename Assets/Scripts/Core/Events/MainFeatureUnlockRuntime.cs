using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.UI;

public class MainFeatureUnlockRuntime : MonoBehaviour
{
    // Chinh level mo khoa nut o day.
    public const int InventoryButtonLevel = 2;
    public const int MiniMapButtonLevel = 2;
    public const int ShopButtonLevel = 3;
    public const int GachaButtonLevel = 4;
    public const int SkillButtonLevel = 5;
    public const int GuildButtonLevel = 5;

    private readonly Dictionary<string, GameObject> cachedObjects = new();

    private void Start()
    {
        CacheObjects();
        LoadLocalLevel();
        Apply();
        RefreshLevelFromApi();
        WorldRuntimeEvents.LevelChanged += Apply;
        WorldRuntimeEvents.QuestsChanged += RefreshLevelFromApi;
    }

    private void OnDestroy()
    {
        WorldRuntimeEvents.LevelChanged -= Apply;
        WorldRuntimeEvents.QuestsChanged -= RefreshLevelFromApi;
    }

    private void LoadLocalLevel()
    {
        WorldState.PlayerLevel = Mathf.Max(1, PlayerPrefs.GetInt(ApiConfig.PlayerLevelKey, WorldState.PlayerLevel));
    }

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

    private void Apply()
    {
        CacheObjects();
        var level = Mathf.Max(1, WorldState.PlayerLevel);

        SetFeatureVisible("InventoryButton", "InventoryPanel", level >= InventoryButtonLevel);
        SetFeatureVisible("MiniMapButton", "MiniMap", level >= MiniMapButtonLevel);
        SetFeatureVisible("ShopButton", "ShopPanel", level >= ShopButtonLevel);
        SetFeatureVisible("GachaButton", "GachaPanel", level >= GachaButtonLevel);
        SetFeatureVisible("SkillButton", "SkillPanel", level >= SkillButtonLevel);
        SetFeatureVisible("GuildButton", "GuildPanel", level >= GuildButtonLevel);

        var questTracker = FindSceneObject("QuestTracker");
        if (questTracker != null)
            questTracker.SetActive(true);

        ApplyButtonGroupVisibility("BottomRightMenu");
    }

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

    private void SetVisible(string objectName, bool visible)
    {
        var target = FindSceneObject(objectName);
        if (target != null)
            target.SetActive(visible);
    }

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

    private void CacheObject(string objectName)
    {
        if (cachedObjects.ContainsKey(objectName) && cachedObjects[objectName] != null)
            return;

        var found = FindSceneObjectSlow(objectName);
        if (found != null)
            cachedObjects[objectName] = found;
    }

    private GameObject FindSceneObject(string objectName)
    {
        CacheObject(objectName);
        return cachedObjects.TryGetValue(objectName, out var target) ? target : null;
    }

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
