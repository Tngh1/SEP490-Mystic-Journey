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
    public GameObject chestPanel;

    // 1. Thêm biến chứa BestiaryPanel
    public GameObject bestiaryPanel;

    private GameObject currentPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // CHỈ GỌI 1 LẦN DUY NHẤT TẠI ĐÂY KHI BẮT ĐẦU GAME
        BindPanels();

        EnsureRuntimeComponents();
        KeepQuestTrackerVisible();

        var settings = Resources.FindObjectsOfTypeAll<MysticJourney.Screen.GameSetting.GameSettingUIManager>();
        foreach (var s in settings)
        {
            if (s != null) s.ForceInitialize();
        }
    }

    private void Start()
    {
        CloseAll();
        KeepQuestTrackerVisible();
        EnsurePlayerHUDController();

        // Không cần tải skill ở đây nữa vì đã chuyển sang PlayerCombat.Start()
    }

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

    public void ShowPanel(GameObject panel)
    {
        if (panel == null)
            return;

        CloseAll();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling(); // Vẫn giữ lại để Panel nổi lên trên các Panel khác
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
        OpenPanel(skillPanel);
    }

    public void OpenQuestPanel()
    {
        if (MainQuestPanelRuntime.Instance != null)
            MainQuestPanelRuntime.Instance.OpenQuestPanel();
        else
            OpenPanel(questPanel);
    }

    public void OpenNpcPanel()
    {
        OpenPanel(npcPanel);
    }

    // 2. Thêm hàm mở BestiaryPanel
    public void OpenBestiaryPanel()
    {
        OpenPanel(bestiaryPanel);
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
        EnsurePanelRuntime<InventoryManager>(inventoryPanel, "InventoryPanel");
        EnsurePanelRuntime<DailyLoginPanelRuntime>(dailyPanel, "DailyPanel", "Login30daysGiftPanel");
        EnsurePanelRuntime<UIChestRewardPanel>(chestPanel, "ChestPanel");
        EnsurePanelRuntime<UIDungeonRoomPanel>(dungeonPanel, "TeamPanel");

        if (DungeonManager.Instance == null)
        {
            var dmObj = new GameObject("DungeonManager");
            dmObj.AddComponent<DungeonManager>();
        }
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
            if (component != null && component.gameObject.scene.IsValid() && !string.IsNullOrEmpty(component.gameObject.scene.name))
                return;
        }

        gameObject.AddComponent<T>();
    }

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

    // 3. Khai báo panel vào danh sách để nó tự đóng khi gọi CloseAll()
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

    // 4. Bind tên gameObject ngoài scene tự động nếu chưa kéo vào Inspector
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
        dungeonPanel = BindPanel(dungeonPanel, "TeamPanel");
        friendPanel = BindPanel(friendPanel, "FriendPanel");
        mailboxPanel = BindPanel(mailboxPanel, "MailboxPanel");
        settingPanel = BindPanel(settingPanel, "GameSettingPanel");
        npcPanel = BindPanel(npcPanel, "NPCPanel");
        chestPanel = BindPanel(chestPanel, "ChestPanel");
        bestiaryPanel = BindPanel(bestiaryPanel, "BestiaryPanel");
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
            if (obj != null && obj.name == objectName && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
                return obj;
        }

        return null;
    }

    private void EnsurePlayerHUDController()
    {
        var hudGo = FindSceneObject("HUD");
        if (hudGo != null)
        {
            if (hudGo.GetComponent<PlayerHUDController>() == null)
            {
                hudGo.AddComponent<PlayerHUDController>();
            }
        }
        else
        {
            Debug.LogWarning("[UIManager] HUD GameObject not found in scene.");
        }
    }
}