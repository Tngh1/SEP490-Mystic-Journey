using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    /// <summary>
    /// True while any tracked panel is on screen. Read by <see cref="GameplayInputProvider"/>
    /// to suppress gameplay hotkeys, so a key pressed while a panel is up doesn't also
    /// leak into the world (pressing "1" over the party roster must not cast a skill).
    /// Uses activeInHierarchy: a panel under a disabled parent isn't actually visible.
    /// </summary>
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

    /// <summary>True if this specific panel is currently on screen.</summary>
    public bool IsPanelOpen(GameObject panel) => panel != null && panel.activeInHierarchy;

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

        MysticJourney.Core.Services.AudioManager.Instance.PlayOpenPanel();
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

    // 👇 ĐÃ BỔ SUNG: Hàm mở GachaPanel
    public void OpenGachaPanel()
    {
        OpenPanel(gachaPanel);
    }

    // 2. Thêm hàm mở BestiaryPanel
    public void OpenBestiaryPanel()
    {
        OpenPanel(bestiaryPanel);
    }

    public void OpenFriendPanel()
    {
        OpenPanel(friendPanel);
    }

    private void KeepQuestTrackerVisible()
    {
        var questTracker = FindSceneObject("QuestTracker");
        if (questTracker == null)
            return;

        // QuestTracker nằm dưới HUD/NonCombatActionGroup — đúng nhóm mà
        // ToggleDungeonMode(true) TẮT để ẩn cụm nút/tab bên trái khi vào hầm ngục.
        // SetParentsActiveForTracker bật lại MỌI cha cho tới HUD, nên mỗi lần
        // mở/đóng panel trong hầm ngục (ShowPanel/ClosePanel/CloseAll đều gọi hàm này)
        // sẽ bật lại NonCombatActionGroup và mấy tab panel bên trái hiện lại giữa hầm ngục.
        // Trong hầm ngục thì quest tracker cũng không có nghĩa, nên bỏ qua hẳn.
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
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
        EnsureRuntime<AchievementPopupRuntime>();
        EnsureRuntime<MainNpcPanelRuntime>();
        EnsureRuntime<PlayerUIHotkeys>();
        EnsurePanelRuntime<InventoryManager>(inventoryPanel, "InventoryPanel");
        EnsurePanelRuntime<DailyLoginPanelRuntime>(dailyPanel, "DailyPanel", "Login30daysGiftPanel");
        EnsurePanelRuntime<UIChestRewardPanel>(chestPanel, "ChestPanel");
        EnsurePanelRuntime<UIPartyPanel>(dungeonPanel, "TeamPanel");
        EnsureButtonHoverEffects();

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

    /// <summary>
    /// Gắn <see cref="UIHoverScaleEffect"/> cho MỌI Button/Toggle có sẵn trong scene, một lần lúc Awake.
    ///
    /// Vòng quét này phủ MỌI button dựng sẵn trong scene, nên panel mới không cần tự viết
    /// lại vòng AddComponent cho các button tĩnh của nó. Vòng quét riêng ở từng panel giờ
    /// chỉ còn cần cho button chúng tự Instantiate lúc runtime (xem ponytail bên dưới).
    ///
    /// Dùng Resources.FindObjectsOfTypeAll thay cho FindObjectsByType: popup dưới
    /// Canvas/PopupLayer đều đang tắt lúc Awake theo thiết kế, FindObjectsByType sẽ bỏ sót hết.
    /// Bù lại nó cũng trả về prefab asset, nên phải lọc scene.IsValid() — thiếu bước này là
    /// AddComponent thẳng vào file prefab trong Assets/.
    /// </summary>
    // ponytail: chỉ quét một lần lúc Awake nên button Instantiate sau đó KHÔNG được phủ.
    // Hiện 16 panel tự gọi AddComponent cho dòng/ô chúng sinh lúc runtime (entry bạn bè,
    // slot guild, ô shop/inventory/daily) — đó là phần vòng quét này không thay thế được.
    // Nâng cấp: class đã nằm ở file riêng nên gắn sẵn được vào từng prefab dòng/ô qua
    // Inspector, bỏ dần 16 vòng đó; hoặc một EventTrigger dùng chung đặt ở Canvas.
    private static void EnsureButtonHoverEffects()
    {
        // Quét Selectable chứ không phải Button: Toggle KHÔNG kế thừa Button (cả hai đều là
        // con của Selectable), nên vòng quét cũ theo Button bỏ sót toàn bộ tab/filter dạng
        // Toggle — 3 tab QuestPanel, 2 tab + 9 filter InventoryPanel, ToggleRequireApproval
        // của GuildPanel đều không có hover trong khi nút thường ngay bên cạnh thì có.
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

            // Item trong Template của Dropdown được sinh/huỷ lại mỗi lần bung danh sách,
            // và lúc Awake nó chỉ là mẫu đang tắt — gắn hover vào mẫu này vừa vô ích vừa
            // làm mỗi dòng trong danh sách bung ra phình lên khi chuột quét qua.
            if (selectable.GetComponentInParent<TMPro.TMP_Dropdown>(true) != null)
                continue;

            // BackgroundBlocker (con của ReportConfirmPopup và PlayerContextMenu) là lớp
            // chặn click phủ TOÀN màn hình — anchor 0,0→1,1 — và nó cũng là Button nên rơi
            // vào vòng quét này. Gắn hover vào đó thì chỉ cần đưa chuột vào bất kỳ đâu trên
            // màn hình là cả lớp phủ phình lên 1.08. UIPlayerContextMenu.EnsureHoverEffects
            // né sẵn chuyện này bằng cách gắn tay cho đúng 3 nút thay vì quét cả cây.
            if (go.name == "BackgroundBlocker")
                continue;

            if (go.GetComponent<UIHoverScaleEffect>() == null)
                go.AddComponent<UIHoverScaleEffect>();
        }
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
