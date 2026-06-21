using System.Collections.Generic;
using UnityEngine;

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
    public GameObject questPanel;
    public GameObject chatPanel;
    public GameObject dungeonPanel;
    public GameObject friendPanel;
    public GameObject mailboxPanel;
    public GameObject settingPanel;
    public GameObject npcPanel;

    private GameObject currentPanel;

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
    }

    private void Start()
    {
        BindPanels();
        CloseAll();
        KeepQuestTrackerVisible();
    }

    public void OpenPanel(GameObject panel)
    {
        if (panel == null)
            return;

        BindPanels();

        if (currentPanel == panel && panel.activeSelf)
        {
            CloseCurrentPanel();
            return;
        }

        ShowPanel(panel);
    }

    public void ShowPanel(GameObject panel)
    {
        if (panel == null)
            return;

        BindPanels();
        CloseAll();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        currentPanel = panel;
        KeepQuestTrackerVisible();
    }

    public void CloseCurrentPanel()
    {
        if (currentPanel != null)
        {
            currentPanel.SetActive(false);
            currentPanel = null;
        }

        KeepQuestTrackerVisible();
    }

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

    public void CloseAll()
    {
        BindPanels();

        foreach (var panel in GetPanels())
        {
            if (panel != null)
                panel.SetActive(false);
        }

        currentPanel = null;
        KeepQuestTrackerVisible();
    }

    public void OpenSkillPanel()
    {
        BindPanels();
        OpenPanel(skillPanel);
    }

    public void OpenQuestPanel()
    {
        BindPanels();

        if (MainQuestPanelRuntime.Instance != null)
            MainQuestPanelRuntime.Instance.OpenQuestPanel();
        else
            OpenPanel(questPanel);
    }

    public void OpenNpcPanel()
    {
        BindPanels();
        OpenPanel(npcPanel);
    }

    private void KeepQuestTrackerVisible()
    {
        var questTracker = FindSceneObject("QuestTracker");
        if (questTracker == null)
            return;

        SetParentsActiveForTracker(questTracker.transform);
        questTracker.SetActive(true);
    }

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

    private void EnsureRuntimeComponents()
    {
        EnsureQuestManager();
        EnsureRuntime<MainQuestPanelRuntime>();
        EnsureRuntime<MainNpcPanelRuntime>();
    }

    private void EnsureQuestManager()
    {
        if (QuestManager.Instance != null)
            return;

        var existing = Resources.FindObjectsOfTypeAll<QuestManager>();
        foreach (var manager in existing)
        {
            if (manager != null && manager.gameObject.scene.IsValid())
                return;
        }

        var questManagerObject = new GameObject("QuestManager");
        questManagerObject.AddComponent<QuestManager>();
    }
    private void EnsureRuntime<T>() where T : Component
    {
        var existing = Resources.FindObjectsOfTypeAll<T>();
        foreach (var component in existing)
        {
            if (component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.name == "Main")
                return;
        }

        gameObject.AddComponent<T>();
    }

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
    }

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
        questPanel = BindPanel(questPanel, "QuestPanel");
        chatPanel = BindPanel(chatPanel, "ChatPanel");
        dungeonPanel = BindPanel(dungeonPanel, "DungeonPanel");
        friendPanel = BindPanel(friendPanel, "FriendPanel");
        mailboxPanel = BindPanel(mailboxPanel, "MailboxPanel");
        settingPanel = BindPanel(settingPanel, "SettingPanel");
        npcPanel = BindPanel(npcPanel, "NPCPanel");
    }

    private static GameObject BindPanel(GameObject current, string objectName)
    {
        return current != null ? current : FindSceneObject(objectName);
    }

    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in objects)
        {
            if (obj != null && obj.name == objectName && obj.scene.IsValid() && obj.scene.name == "Main")
                return obj;
        }

        return null;
    }
}


