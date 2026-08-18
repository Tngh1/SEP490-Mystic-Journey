using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Executes core business logic for mono behaviour.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Panels")]
    public GameObject inventoryPanel;
    public GameObject shopPanel;
    public GameObject skillPanel;
    public GameObject guidePanel;
    public GameObject dialoguePanel;
    public GameObject dailyPanel;
    public GameObject gachaPanel;
    public GameObject mapPanel;
    public GameObject PlayerProfilePanel;
    public GameObject questPanel;
    public GameObject chatPanel;
    public GameObject dungeonPanel;
    public GameObject friendPanel;
    public GameObject mailboxPanel;
    public GameObject settingPanel;
    public GameObject npcPanel;
    public GameObject chestPanel;

    public GameObject bestiaryPanel;

    private GameObject currentPanel;

    // Initializes internal component caches and dependencies for UIManager upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BindPanels();

        EnsureRuntimeComponents();
        KeepQuestTrackerVisible();

        var settings = Resources.FindObjectsOfTypeAll<MysticJourney.Screen.GameSetting.GameSettingUIManager>();
        foreach (var s in settings)
        {
            if (s != null) s.ForceInitialize();
        }
    }

    // Performs startup initialization for UIManager on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        CloseAll();
        KeepQuestTrackerVisible();
        EnsurePlayerHUDController();

    }

    // Per-frame update loop for UIManager.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        OpenSettingsFromEscape();
    }

    // Executes core business logic for open settings from escape.
    private void OpenSettingsFromEscape()
    {
        if (settingPanel == null)
            settingPanel = BindPanel(null, "GameSettingPanel");

        if (settingPanel == null || settingPanel.activeInHierarchy)
            return;

        ShowPanel(settingPanel);
    }


    // Executes core business logic for is any panel open.
    public bool IsAnyPanelOpen
    {
        get
        {
            foreach (var panel in GetPanels())
            {
                if (panel != null && panel.activeInHierarchy)
                    return true;
            }

            return false;
        }
    }

    // Executes core business logic for is panel open.
    // Returns a boolean indicating operation success.
    public bool IsPanelOpen(GameObject panel) => panel != null && panel.activeInHierarchy;

    // Executes core business logic for open panel.
    public void OpenPanel(GameObject panel)
    {
        if (panel == null)
            return;

        if (currentPanel == panel && panel.activeSelf)
        {
            CloseCurrentPanel();
            return;
        }

        ShowPanel(panel);
    }

    // Executes core business logic for show panel.
    public void ShowPanel(GameObject panel)
    {
        if (panel == null)
            return;

        CloseAll();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        currentPanel = panel;
        KeepQuestTrackerVisible();

        MysticJourney.Core.Services.AudioManager.Instance.PlayOpenPanel();
    }

    // Executes core business logic for close current panel.
    public void CloseCurrentPanel()
    {
        if (currentPanel != null)
        {
            currentPanel.SetActive(false);
            currentPanel = null;
        }

        KeepQuestTrackerVisible();
    }

    // Executes core business logic for close panel.
    public void ClosePanel(GameObject panel)
    {
        if (panel == null)
            return;

        if (panel.activeSelf)
            panel.SetActive(false);

        if (currentPanel == panel)
            currentPanel = null;

        KeepQuestTrackerVisible();
    }

    // Executes core business logic for close all.
    public void CloseAll()
    {
        foreach (var panel in GetPanels())
        {
            if (panel != null)
                panel.SetActive(false);
        }

        currentPanel = null;
        KeepQuestTrackerVisible();
    }

    // Update visibility for skill panel; it updates navigation or visibility through open panel.
    public void OpenSkillPanel()
    {
        OpenPanel(skillPanel);
    }

    // Executes core business logic for open quest panel.
    public void OpenQuestPanel()
    {
        if (MainQuestPanelRuntime.Instance != null)
            MainQuestPanelRuntime.Instance.OpenQuestPanel();
        else
            OpenPanel(questPanel);
    }

    // Update visibility for npc panel; it updates navigation or visibility through open panel.
    public void OpenNpcPanel()
    {
        OpenPanel(npcPanel);
    }

    // Update visibility for gacha panel; it updates navigation or visibility through open panel.
    public void OpenGachaPanel()
    {
        OpenPanel(gachaPanel);
    }

    // Update visibility for bestiary panel; it updates navigation or visibility through open panel.
    public void OpenBestiaryPanel()
    {
        OpenPanel(bestiaryPanel);
    }

    // Update visibility for friend panel; it updates navigation or visibility through open panel.
    public void OpenFriendPanel()
    {
        OpenPanel(friendPanel);
    }

    // Executes core business logic for keep quest tracker visible.
    private void KeepQuestTrackerVisible()
    {
        var questTracker = FindSceneObject("QuestTracker");
        if (questTracker == null)
            return;

        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        SetParentsActiveForTracker(questTracker.transform);
        questTracker.SetActive(true);
    }

    // Executes core business logic for set parents active for tracker.
    private static void SetParentsActiveForTracker(Transform child)
    {
        var parents = new Stack<Transform>();
        var current = child == null ? null : child.parent;

        while (current != null)
        {
            parents.Push(current);
            if (current.name == "HUD" || current.name == "Canvas")
                break;
            current = current.parent;
        }

        while (parents.Count > 0)
        {
            var parent = parents.Pop();
            if (parent != null && !parent.gameObject.activeSelf)
                parent.gameObject.SetActive(true);
        }
    }

    // Executes core business logic for ensure runtime components.
    private void EnsureRuntimeComponents()
    {
        EnsureQuestManager();
        EnsureRuntime<MainQuestPanelRuntime>();
        EnsureRuntime<AchievementPopupRuntime>();
        EnsureRuntime<MainNpcPanel>();
        EnsureRuntime<PlayerUIHotkeys>();
        EnsurePanelRuntime<InventoryUIManager>(inventoryPanel, "InventoryPanel");
        EnsurePanelRuntime<DailyLoginUIManager>(dailyPanel, "DailyPanel", "Login30daysGiftPanel");
        EnsurePanelRuntime<UIChestRewardPanel>(chestPanel, "ChestPanel");
        EnsurePanelRuntime<PartyPanel>(dungeonPanel, "TeamPanel");
        EnsureButtonHoverEffects();

        if (DungeonManager.Instance == null)
        {
            var dmObj = new GameObject("DungeonManager");
            dmObj.AddComponent<DungeonManager>();
        }
    }

    // Executes core business logic for ensure quest manager.
    private void EnsureQuestManager()
    {
        if (QuestUIManager.Instance != null)
            return;

        var existing = Resources.FindObjectsOfTypeAll<QuestUIManager>();
        foreach (var manager in existing)
        {
            if (manager != null && manager.gameObject.scene.IsValid())
                return;
        }

        var questManagerObject = new GameObject("QuestUIManager");
        questManagerObject.AddComponent<QuestUIManager>();
    }

    // Executes core business logic for component.
    // Logic details: validates required non-empty string arguments.
    private void EnsureRuntime<T>() where T : Component
    {
        var existing = Resources.FindObjectsOfTypeAll<T>();
        foreach (var component in existing)
        {
            if (component != null && component.gameObject.scene.IsValid() && !string.IsNullOrEmpty(component.gameObject.scene.name))
                return;
        }

        gameObject.AddComponent<T>();
    }

    // Executes core business logic for component.
    // Logic details: validates required non-empty string arguments.
    private void EnsurePanelRuntime<T>(GameObject panel, params string[] panelNames) where T : Component
    {
        var existing = Resources.FindObjectsOfTypeAll<T>();
        foreach (var component in existing)
        {
            if (component != null && component.gameObject.scene.IsValid() && !string.IsNullOrEmpty(component.gameObject.scene.name))
                return;
        }

        if (panel == null && panelNames != null)
        {
            foreach (var panelName in panelNames)
            {
                panel = FindSceneObject(panelName);
                if (panel != null)
                    break;
            }
        }

        if (panel != null)
            panel.AddComponent<T>();
    }

    // Executes core business logic for ensure button hover effects.
    // Logic details: validates required non-empty string arguments.
    private static void EnsureButtonHoverEffects()
    {
        var selectables = Resources.FindObjectsOfTypeAll<Selectable>();
        foreach (var selectable in selectables)
        {
            if (selectable == null)
                continue;

            if (!(selectable is Button || selectable is Toggle))
                continue;

            var go = selectable.gameObject;
            if (!go.scene.IsValid() || string.IsNullOrEmpty(go.scene.name))
                continue;

            if (selectable.GetComponentInParent<TMPro.TMP_Dropdown>(true) != null)
                continue;

            if (go.name == "BackgroundBlocker")
                continue;

            if (go.GetComponent<UIHoverScaleEffect>() == null)
                go.AddComponent<UIHoverScaleEffect>();
        }
    }

    // Executes core business logic for get panels.
    private IEnumerable<GameObject> GetPanels()
    {
        yield return inventoryPanel;
        yield return shopPanel;
        yield return skillPanel;
        yield return guidePanel;
        yield return dialoguePanel;
        yield return dailyPanel;
        yield return gachaPanel;
        yield return mapPanel;
        yield return questPanel;
        yield return chatPanel;
        yield return dungeonPanel;
        yield return friendPanel;
        yield return mailboxPanel;
        yield return settingPanel;
        yield return npcPanel;
        yield return chestPanel;
        yield return bestiaryPanel;
    }

    // Executes core business logic for bind panels.
    private void BindPanels()
    {
        inventoryPanel = BindPanel(inventoryPanel, "InventoryPanel");
        shopPanel = BindPanel(shopPanel, "ShopPanel");
        skillPanel = BindPanel(skillPanel, "SkillPanel");
        guidePanel = BindPanel(guidePanel, "GuidePanel");
        dialoguePanel = BindPanel(dialoguePanel, "DialoguePanel");
        dailyPanel = BindPanel(dailyPanel, "DailyPanel") ?? BindPanel(dailyPanel, "Login30daysGiftPanel");
        gachaPanel = BindPanel(gachaPanel, "GachaPanel");
        mapPanel = BindPanel(mapPanel, "MapPanel");
        PlayerProfilePanel = BindPanel(PlayerProfilePanel, "PlayerProfilePanel");
        questPanel = BindPanel(questPanel, "QuestPanel");
        chatPanel = BindPanel(chatPanel, "ChatPanel");
        dungeonPanel = BindPanel(dungeonPanel, "TeamPanel");
        friendPanel = BindPanel(friendPanel, "FriendPanel");
        mailboxPanel = BindPanel(mailboxPanel, "MailboxPanel");
        settingPanel = BindPanel(settingPanel, "GameSettingPanel");
        npcPanel = BindPanel(npcPanel, "NPCPanel") ?? BindPanel(npcPanel, "MainNpcPanel");
        chestPanel = BindPanel(chestPanel, "ChestPanel");
        bestiaryPanel = BindPanel(bestiaryPanel, "BestiaryPanel");
    }

    // Executes core business logic for bind panel.
    // Logic details: validates required non-empty string arguments.
    private static GameObject BindPanel(GameObject current, string objectName)
    {
        return current != null ? current : FindSceneObject(objectName);
    }

    // Executes core business logic for find scene object.
    // Logic details: validates required non-empty string arguments.
    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in objects)
        {
            if (obj != null && obj.name == objectName && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
                return obj;
        }

        return null;
    }

    // Executes core business logic for ensure player hud controller.
    private void EnsurePlayerHUDController()
    {
        var hudGo = FindSceneObject("HUD");
        if (hudGo != null)
        {
            if (hudGo.GetComponent<PlayerHUDUIManager>() == null)
            {
                hudGo.AddComponent<PlayerHUDUIManager>();
            }
        }
        else
        {
            Debug.LogWarning("[UIManager] HUD GameObject not found in scene.");
        }
    }
}
