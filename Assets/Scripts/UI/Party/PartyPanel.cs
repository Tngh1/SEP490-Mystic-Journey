using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;

// Executes mono behaviour operation.
public class PartyPanel : MonoBehaviour
{
    // Executes instance operation.
    public static PartyPanel Instance { get; private set; }

    [Header("Static References")]
    [SerializeField] private TMP_Text dungeonNameText;
    [SerializeField] private TMP_Text energyCostText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Sprite readyActiveSprite;
    [SerializeField] private Button inviteButton;
    [SerializeField] private Button closeButton;

    [Header("Party Slots (index 0 = host slot). Auto-found under 'Players' if empty.")]
    [SerializeField] private UIPartySlot[] slots;

    [Tooltip("Class → avatar sprite mapping. Auto-loaded from Resources/ClassAvatarDatabase if empty.")]
    [SerializeField] private ClassAvatarDatabaseSO avatarDatabase;

    [Tooltip("Skin → portrait mapping. Auto-loaded from Resources/SkinDatabase if empty.")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

    [Header("Dungeon Info UI")]
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Transform dropsContainer;
    [SerializeField] private GameObject itemSlotPartyPrefab;

    [Header("Runtime Info")]
    private int selectedConfigId = 1;
    private int selectedMapId = MapProgressionRules.FirstMapId;
    private string selectedSceneName = "AbandonedMines";
    private string selectedDungeonName;
    private string selectedDescription = "";
    private System.Collections.Generic.List<ChestItemResponse> possibleDrops = new();
    private int energyCost = 20;
    private int playerEnergy = 0;
    private int goldMinReward = 0;
    private int goldMaxReward = 0;
    private int experienceReward = 0;

    private string localPlayerName = "Player";
    private int localPlayerLevel = 1;

    private GameObject dynamicCanvasObj;
    private GameObject friendModalObj;

    private PartyLobby _hookedParty;
    private Sprite _readyDefaultSprite;
    private readonly List<InviteCooldownView> _inviteCooldownViews = new List<InviteCooldownView>();
    private Coroutine _inviteCooldownCoroutine;

    private sealed class InviteCooldownView
    {
        public Button Button;
        public TMP_Text Label;
        public Image Image;
        public bool IsSent;
    }

    // Initializes singleton reference and hooks party disband presence callback.
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this; // Cache singleton instance
        }
        else if (Instance != this)
        {
            Destroy(this); // Prevent duplicate party panel
            return;
        }

        PlayerPresence.OnPartyDisbanded += HandlePartyDisbanded; // Listen for host disbanding party

        if (readyButton != null && readyButton.image != null)
            _readyDefaultSprite = readyButton.image.sprite; // Cache unready button state sprite

        gameObject.SetActive(false); // Initially hidden
    }

    // Subscribes local party roster changes and state updates.
    private void OnEnable()
    {
        PartyLobby.OnLocalPartyChanged += HandleLocalPartyChanged; // Re-subscribe when joining new party lobby
        RehookPartyEvents(); // Subscribe member roster changes
    }

    // Unsubscribes lobby event listeners when panel is dismissed.
    private void OnDisable()
    {
        PartyLobby.OnLocalPartyChanged -= HandleLocalPartyChanged;
        UnhookPartyEvents();
        ClearInviteCooldownViews();
    }

    // Cleans up event listeners upon GameObject destruction.
    private void OnDestroy()
    {
        if (Instance == this)
        {
            PlayerPresence.OnPartyDisbanded -= HandlePartyDisbanded;
            Instance = null;
        }
    }

    // Displays notification alert and closes party window when host disbands the team.
    private void HandlePartyDisbanded(string hostName)
    {
        string message = string.IsNullOrWhiteSpace(hostName)
            ? "The party has been disbanded by the host."
            : $"{hostName} has disbanded the party.";

        UIPopupBox.Notify(transform, "Party", message); // Notify player

        Close(); // Close panel
    }


    // Subscribes roster and state change events from the active PartyLobby.
    private void RehookPartyEvents()
    {
        UnhookPartyEvents();
        _hookedParty = PartyLobby.Local;
        if (_hookedParty != null)
        {
            _hookedParty.OnRosterChanged += UpdateUI; // Redraw member slot cards
            _hookedParty.OnPartyStateChanged += HandlePartyState; // Check ready / in-dungeon status
        }
    }

    // Unsubscribes active lobby event handlers.
    private void UnhookPartyEvents()
    {
        if (_hookedParty != null)
        {
            _hookedParty.OnRosterChanged -= UpdateUI;
            _hookedParty.OnPartyStateChanged -= HandlePartyState;
            _hookedParty = null;
        }
    }

    // Executes handle local party changed operation.
    private void HandleLocalPartyChanged()
    {
        RehookPartyEvents();
        PublishDungeonSelectionIfHost();
        if (gameObject.activeInHierarchy) UpdateUI();
    }

    // Executes handle party state operation.
    private void HandlePartyState(PartyLobby.PartyState state)
    {
        if (state != PartyLobby.PartyState.Lobby)
        {
            Close();
        }
    }


    // Executes open for dungeon operation.
    public void OpenForDungeon(int configId, string sceneName, int cost, string displayName, int requiredMapId = 0)
    {
        selectedConfigId = configId;
        selectedMapId = requiredMapId > 0
            ? requiredMapId
            : MapProgressionRules.GetMapId(WorldState.CurrentMapName);
        selectedSceneName = sceneName;
        energyCost = cost;
        selectedDungeonName = displayName;

        if (configId <= 0)
        {
            gameObject.SetActive(true);
            FetchPlayerEnergy();
            return;
        }

        DungeonApi.Instance.GetById(configId,
            response =>
            {
                if (response != null)
                {
                    energyCost = response.EnergyCost;
                    if (!string.IsNullOrEmpty(response.Name))
                        selectedDungeonName = response.Name;
                    selectedDescription = response.Description ?? "No description available.";
                    possibleDrops = response.PossibleDrops ?? new();
                    goldMinReward = response.GoldMinReward;
                    goldMaxReward = response.GoldMaxReward;
                    experienceReward = response.ExperienceReward;
                }
                gameObject.SetActive(true);
                PublishDungeonSelectionIfHost();
                FetchPlayerEnergy();
            },
            error =>
            {
                gameObject.SetActive(true);
                PublishDungeonSelectionIfHost();
                FetchPlayerEnergy();
            }
        );
    }

    // Executes publish dungeon selection if host operation.
    private void PublishDungeonSelectionIfHost()
    {
        if (PartyService.IsHost)
            PartyService.SetDungeon(selectedConfigId, selectedSceneName, selectedDungeonName);
    }

    // Executes fetch player energy operation.
    private void FetchPlayerEnergy()
    {
        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                playerEnergy = profile.Energy;
                localPlayerName = profile.DisplayName;
                localPlayerLevel = Mathf.Max(1, profile.Level);
                UpdateUI();
            },
            error =>
            {
                Debug.LogWarning($"[PartyPanel] GetMyProfile failed: {error.Message}");
                playerEnergy = 100;
                localPlayerName = WorldState.PlayerName ?? "Player";
                localPlayerLevel = Mathf.Max(1, WorldState.PlayerLevel);
                UpdateUI();
            }
        );
    }


    // Executes update ui operation.
    private void UpdateUI()
    {
        if (transform.Find("Players") == null)
        {
            Debug.LogError("[PartyPanel] Players child not found on PartyPanel! Designed UI hierarchy is incorrect.");
            return;
        }

        try
        {
            FindReferences();

            var party = PartyLobby.Local;
            bool nonHostMember = party != null && !party.IsLocalHost;

            string dungeonName = selectedDungeonName;
            if (nonHostMember && !string.IsNullOrEmpty(party.DungeonName.Value))
                dungeonName = party.DungeonName.Value;

            if (dungeonNameText != null)
            {
                dungeonNameText.text = string.IsNullOrWhiteSpace(dungeonName) ? "Dungeon" : dungeonName;
                dungeonNameText.textWrappingMode = TextWrappingModes.NoWrap;
                dungeonNameText.enableAutoSizing = true;
                dungeonNameText.fontSizeMax = 48;
                dungeonNameText.fontSizeMin = 18;
            }

            UpdateDungeonInfoPanel();
            UpdateEnergyCostLabel();
            UpdatePlayersPanel();
            UpdateBottomBar();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    // Executes update dungeon info panel operation.
    private void UpdateDungeonInfoPanel()
    {
        if (descriptionText != null)
        {
            descriptionText.text = selectedDescription;
        }

        if (dropsContainer != null)
        {
            foreach (Transform child in dropsContainer)
            {
                // Destroy is deferred until the end of the frame. Disable old slots first so
                // a roster/profile refresh cannot lay out both generations on top of each other.
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            int dropCount = 0;
            if (goldMaxReward > 0) dropCount++;
            if (experienceReward > 0) dropCount++;
            if (possibleDrops != null) dropCount += possibleDrops.Count;

            ConfigureDropsScroll(dropCount);

            if (goldMaxReward > 0)
            {
                SpawnDropItem("Gold", goldMinReward, goldMaxReward, null);
            }

            if (experienceReward > 0)
            {
                SpawnDropItem("Exp", experienceReward, experienceReward, null);
            }

            if (possibleDrops != null)
            {
                foreach (var drop in possibleDrops)
                {
                    SpawnDropItem(drop.ItemName, drop.QuantityMin, drop.QuantityMax, drop.ItemIconUrl);
                }
            }
        }
    }

    private const float DropSlotSize = 100f;

    // Configures the drops container to support 100x100 slots and horizontal scrolling
    // via ScrollRect when the number of items exceeds the viewport width.
    private void ConfigureDropsScroll(int itemCount)
    {
        if (dropsContainer == null) return;

        // Determine Viewport (DungeonDrop) and Content (RewardItem / Content)
        Transform viewport;
        Transform content;

        if (dropsContainer.name.Equals("DungeonDrop", StringComparison.OrdinalIgnoreCase))
        {
            viewport = dropsContainer;
            content = dropsContainer.Find("RewardItem") ?? dropsContainer.Find("Content");
            if (content == null)
            {
                var contentGo = new GameObject("Content", typeof(RectTransform));
                contentGo.transform.SetParent(viewport, false);
                content = contentGo.transform;
            }
            dropsContainer = content;
        }
        else
        {
            content = dropsContainer;
            viewport = dropsContainer.parent;
        }

        if (viewport == null || content == null) return;

        // Configure ScrollRect on Viewport (DungeonDrop) without changing Viewport's own transform
        var scrollRect = viewport.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 25f;
        }
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.viewport = viewport as RectTransform;
        scrollRect.content = content as RectTransform;
        scrollRect.horizontalNormalizedPosition = 0f;

        if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        // Remove any legacy GridLayoutGroup immediately so HorizontalLayoutGroup can be added without conflict
        var oldGridContent = content.GetComponent<GridLayoutGroup>();
        if (oldGridContent != null) DestroyImmediate(oldGridContent);

        var oldGridViewport = viewport.GetComponent<GridLayoutGroup>();
        if (oldGridViewport != null) DestroyImmediate(oldGridViewport);

        // Configure HorizontalLayoutGroup on Content
        var hg = content.GetComponent<HorizontalLayoutGroup>();
        if (hg == null)
        {
            hg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        if (hg != null)
        {
            hg.padding = new RectOffset(12, 12, 8, 8);
            hg.spacing = 10f;
            hg.childAlignment = TextAnchor.MiddleCenter;
            hg.childControlWidth = false;
            hg.childControlHeight = false;
            hg.childForceExpandWidth = false;
            hg.childForceExpandHeight = false;
        }

        // ContentSizeFitter allows Content width to expand with items inside Viewport
        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        // Position Content inside Viewport
        var contentRt = content as RectTransform;
        var viewportRt = viewport as RectTransform;
        if (contentRt != null && viewportRt != null)
        {
            float viewportWidth = viewportRt.rect.width > 1f ? viewportRt.rect.width : 376f;
            float totalWidth = itemCount * DropSlotSize + Mathf.Max(0, itemCount - 1) * 10f + 24f;

            if (totalWidth <= viewportWidth)
            {
                // Center content inside the Viewport box
                contentRt.anchorMin = new Vector2(0.5f, 0.5f);
                contentRt.anchorMax = new Vector2(0.5f, 0.5f);
                contentRt.pivot = new Vector2(0.5f, 0.5f);
                contentRt.anchoredPosition = Vector2.zero;
            }
            else
            {
                // Left-align content inside the Viewport box so player scrolls to the right
                contentRt.anchorMin = new Vector2(0f, 0.5f);
                contentRt.anchorMax = new Vector2(0f, 0.5f);
                contentRt.pivot = new Vector2(0f, 0.5f);
                contentRt.anchoredPosition = Vector2.zero;
            }
        }
    }

    // Spawns a drop item slot using the ItemSlotParty prefab (100x100) and populates it
    // via UIBaseItemSlot.SetupCustom.
    private void SpawnDropItem(string itemName, int minQty, int maxQty, string iconUrl)
    {
        EnsureItemSlotPartyPrefab();

        GameObject itemObj;
        if (itemSlotPartyPrefab != null)
        {
            itemObj = Instantiate(itemSlotPartyPrefab, dropsContainer);
        }
        else
        {
            itemObj = new GameObject("DropItem", typeof(RectTransform), typeof(Image));
            itemObj.transform.SetParent(dropsContainer, false);
            var fallbackRt = itemObj.GetComponent<RectTransform>();
            fallbackRt.sizeDelta = new Vector2(DropSlotSize, DropSlotSize);
            Debug.LogWarning("[PartyPanel] ItemSlotParty prefab not found. Falling back to raw GameObject.");
        }

        var rt = itemObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(DropSlotSize, DropSlotSize);
        }

        // Resolve icon sprite
        Sprite sprite = ResolveDropIcon(itemName, iconUrl);

        // Build quantity text
        string qtyText = minQty == maxQty ? $"x{maxQty}" : $"x{minQty}-{maxQty}";

        // Use UIBaseItemSlot.SetupCustom if the prefab has one (ItemSlotParty has UIRewardSlot)
        var slot = itemObj.GetComponent<UIBaseItemSlot>();
        if (slot != null)
        {
            slot.SetupCustom(itemName, qtyText, sprite);
        }
        else
        {
            // Manual fallback for prefabs without UIBaseItemSlot
            var iconTransform = itemObj.transform.Find("Icon");
            Image image = iconTransform != null
                ? iconTransform.GetComponent<Image>()
                : itemObj.GetComponentInChildren<Image>();

            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                image.enabled = true;
                image.preserveAspect = true;
            }
            else if (image != null)
            {
                image.enabled = false;
            }

            var label = itemObj.GetComponentInChildren<TMPro.TMP_Text>();
            if (label != null)
            {
                label.text = qtyText;
            }
        }
    }

    // Resolves the icon sprite for a drop item by checking ItemIconDatabase,
    // then iconUrl, then Resources paths for known items.
    private static Sprite ResolveDropIcon(string itemName, string iconUrl)
    {
        Sprite sprite = null;

        if (!string.IsNullOrEmpty(itemName) && ItemIconDatabase.Instance != null)
            sprite = ItemIconDatabase.Instance.GetIcon(itemName, null);

        if (sprite == null && !string.IsNullOrEmpty(iconUrl))
            sprite = Resources.Load<Sprite>(iconUrl);

        if (sprite == null && !string.IsNullOrEmpty(itemName))
            sprite = Resources.Load<Sprite>("Item/" + itemName);

        if (sprite == null && !string.IsNullOrEmpty(itemName))
        {
            if (itemName.Equals("Gold", StringComparison.OrdinalIgnoreCase))
                sprite = Resources.Load<Sprite>("Item/Gold-Icon");
            else if (itemName.Equals("Exp", StringComparison.OrdinalIgnoreCase) ||
                     itemName.Equals("Experience", StringComparison.OrdinalIgnoreCase))
                sprite = Resources.Load<Sprite>("Item/Exp-icon");
        }

        if (sprite == null && !string.IsNullOrEmpty(itemName))
            Debug.LogWarning($"[PartyPanel] Không tìm thấy hình ảnh cho item: {itemName}");

        return sprite;
    }

    // Loads ItemSlotParty prefab from Resources if not already assigned via Inspector.
    private void EnsureItemSlotPartyPrefab()
    {
        if (itemSlotPartyPrefab != null) return;
        itemSlotPartyPrefab = Resources.Load<GameObject>("ItemSlotParty");
    }

    // Executes update energy cost label operation.
    private void UpdateEnergyCostLabel()
    {
        if (energyCostText == null) return;
        energyCostText.text = $"-{energyCost}";
        energyCostText.color = playerEnergy >= energyCost
            ? new Color(0.18f, 0.8f, 0.25f)
            : new Color(0.9f, 0.2f, 0.2f);
    }

    // Executes find references operation.
    private void FindReferences()
    {
        Transform headerTrans = transform.Find("Header");
        if (headerTrans != null)
        {
            Transform exitBtnTrans = headerTrans.Find("ExitButton");
            if (exitBtnTrans != null)
            {
                closeButton = exitBtnTrans.GetComponent<Button>();
                if (closeButton != null)
                {
                    closeButton.onClick.RemoveAllListeners();
                    closeButton.onClick.AddListener(OnExitClick);
                    AddHoverEffect(closeButton.gameObject);
                }
            }

            Transform dungeonNameTrans = headerTrans.Find("DungeonName");
            if (dungeonNameTrans != null)
            {
                dungeonNameText = dungeonNameTrans.GetComponentInChildren<TMP_Text>(true);
            }
        }

        Transform bottomTrans = transform.Find("BottomSection");
        if (bottomTrans != null)
        {
            Transform startBtnTrans = bottomTrans.Find("StartButton");
            if (startBtnTrans != null)
            {
                startButton = startBtnTrans.GetComponent<Button>();
                AddHoverEffect(startButton != null ? startButton.gameObject : startBtnTrans.gameObject);
            }

            Transform readyBtnTrans = bottomTrans.Find("ReadyButton");
            if (readyBtnTrans != null)
            {
                readyButton = readyBtnTrans.GetComponent<Button>();
                if (_readyDefaultSprite == null && readyButton != null && readyButton.image != null)
                    _readyDefaultSprite = readyButton.image.sprite;
                AddHoverEffect(readyButton != null ? readyButton.gameObject : readyBtnTrans.gameObject);
            }

            Transform inviteBtnTrans = bottomTrans.Find("InviteButton");
            if (inviteBtnTrans != null)
            {
                inviteButton = inviteBtnTrans.GetComponent<Button>();
                if (inviteButton != null)
                {
                    inviteButton.onClick.RemoveAllListeners();
                    inviteButton.onClick.AddListener(OpenFriendListModal);
                    AddHoverEffect(inviteButton.gameObject);
                }
            }

            Transform energyTrans = bottomTrans.Find("EnergyCost");
            if (energyTrans != null)
            {
                energyCostText = energyTrans.GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (descriptionText == null)
        {
            var allTexts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in allTexts)
            {
                if (descriptionText == null && t.text == "New Text" && t.name.Contains("Text"))
                {
                    descriptionText = t;
                }
            }
        }

        if (dropsContainer == null)
        {
            var layouts = GetComponentsInChildren<UnityEngine.UI.LayoutGroup>(true);
            foreach (var layout in layouts)
            {
                if (dropsContainer == null)
                {
                    string lname = layout.name.ToLower();
                    if (lname.Contains("drop") || lname.Contains("item") || lname.Contains("content") || lname.Contains("container"))
                    {
                        dropsContainer = layout.transform;
                        if (lname.Contains("drop") || lname.Contains("item")) break;
                    }
                }
            }
        }
    }


    // Executes update players panel operation.
    private void UpdatePlayersPanel()
    {
        Transform playersTrans = transform.Find("Players");
        if (playersTrans == null) return;

        EnsureSlotsResolved();
        if (slots == null || slots.Length == 0) return;

        var party = PartyLobby.Local;
        bool localIsHost = party != null && party.IsLocalHost;

        if (slots.Length > 0 && slots[0] != null)
        {
            string hostName; int hostLevel; CharacterClass hostCls; int hostSkin;
            if (party != null && TryGetHostMember(party, out var hostMember))
            {
                hostName = hostMember.Name.Value;
                hostLevel = hostMember.Level;
                hostCls = (CharacterClass)hostMember.PlayerClass;
                hostSkin = hostMember.SkinId;
            }
            else
            {
                hostName = localPlayerName;
                hostLevel = localPlayerLevel;
                if (!Enum.TryParse(WorldState.PlayerClass ?? "Knight", true, out hostCls))
                    hostCls = CharacterClass.Knight;
                hostSkin = WorldState.EquippedSkinId;
            }
            slots[0].RenderHost(hostName, hostLevel, hostCls, FlagFor(hostCls), NameplateFor(hostCls), SkinPortraitFor(hostSkin, hostCls));
        }

        var others = new PartyLobby.Member[Mathf.Max(0, slots.Length - 1)];
        int otherCount = 0;
        if (party != null)
        {
            for (int i = 0; i < PartyLobby.MaxMembers && otherCount < others.Length; i++)
            {
                var m = party.Members[i];
                if (m.IsOccupied && m.Player != party.HostPlayer)
                    others[otherCount++] = m;
            }
        }

        for (int s = 1; s < slots.Length; s++)
        {
            // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
            var slot = slots[s];
            if (slot == null) continue;

            int otherIdx = s - 1;
            if (otherIdx < otherCount)
            {
                var m = others[otherIdx];
                var cls = (CharacterClass)m.PlayerClass;
                var target = m.Player;
                var memberName = m.Name.Value;
                slot.RenderMember(memberName, m.Level, cls, FlagFor(cls), NameplateFor(cls), m.Ready,
                    canKick: localIsHost,
                    onKick: () => ConfirmKick(memberName, () => PartyService.KickMember(target)),
                    skinPortrait: SkinPortraitFor(m.SkinId, cls));
            }
            else
            {
                slot.RenderEmpty();
            }
        }
    }

    // Executes confirm kick operation.
    private void ConfirmKick(string memberName, Action onConfirm)
    {
        UIPopupBox.Show(transform, "Party", $"Kick {memberName} from the party?", onConfirm);
    }

    // Executes ensure slots resolved operation.
    private void EnsureSlotsResolved()
    {
        if (slots != null && slots.Length > 0)
        {
            bool anyNull = false;
            for (int i = 0; i < slots.Length; i++) if (slots[i] == null) anyNull = true;
            if (!anyNull) { EnsureAvatarDb(); return; }
        }

        Transform playersTrans = transform.Find("Players");
        if (playersTrans != null)
        {
            var resolved = new System.Collections.Generic.List<UIPartySlot>();
            for (int i = 0; i < playersTrans.childCount; i++)
            {
                var child = playersTrans.GetChild(i);
                if (child == null || !child.name.StartsWith("Player")) continue;
                var comp = child.GetComponent<UIPartySlot>();
                if (comp == null) comp = child.gameObject.AddComponent<UIPartySlot>();
                resolved.Add(comp);

                var rt = child.GetComponent<RectTransform>();
                if (rt != null) rt.localScale = new Vector3(0.6f, 0.6f, 1f);
            }
            slots = resolved.ToArray();

            var layout = playersTrans.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (layout == null) layout = playersTrans.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 10;

            var playersRt = playersTrans.GetComponent<RectTransform>();
            if (playersRt != null)
            {
                playersRt.anchorMin = new Vector2(0, 1);
                playersRt.anchorMax = new Vector2(0, 1);
                playersRt.pivot = new Vector2(0, 1);
                playersRt.anchoredPosition = new Vector2(20, -150);
            }
        }

        EnsureAvatarDb();

        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("[PartyPanel] No party slots found under 'Players'. UI may not display correctly.");
        }
    }

    // Executes ensure avatar db operation.
    private void EnsureAvatarDb()
    {
        if (avatarDatabase == null)
            avatarDatabase = ClassAvatarDatabaseSO.LoadDefault();
    }

    // Executes flag for operation.
    private Sprite FlagFor(CharacterClass cls)
    {
        if (avatarDatabase == null) return null;
        return avatarDatabase.GetFlag(cls);
    }

    // Executes nameplate for operation.
    private Sprite NameplateFor(CharacterClass cls)
    {
        if (avatarDatabase == null) return null;
        return avatarDatabase.GetNameplate(cls);
    }

    // Executes skin portrait for operation.
    // Evaluates conditions and returns a boolean result.
    private Sprite SkinPortraitFor(int skinId, CharacterClass characterClass)
    {
        if (skinDatabase == null) skinDatabase = SkinDatabaseSO.LoadDefault();
        if (skinDatabase == null) return null;
        var equippedPreview = skinId > 0 ? skinDatabase.GetPreviewSprite(skinId) : null;
        return equippedPreview != null ? equippedPreview : skinDatabase.GetDefaultPreviewSprite(characterClass);
    }

    // Executes try get host member operation.
    private static bool TryGetHostMember(PartyLobby party, out PartyLobby.Member host)
    {
        for (int i = 0; i < PartyLobby.MaxMembers; i++)
        {
            var m = party.Members[i];
            if (m.IsOccupied && m.Player == party.HostPlayer)
            {
                host = m;
                return true;
            }
        }
        host = default;
        return false;
    }


    // Executes update bottom bar operation.
    private void UpdateBottomBar()
    {
        var party = PartyLobby.Local;
        bool inParty = party != null;
        bool isHostOrSolo = party == null || party.IsLocalHost;

        if (inviteButton != null)
        {
            inviteButton.gameObject.SetActive(isHostOrSolo);
            inviteButton.interactable = isHostOrSolo;
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(isHostOrSolo);
            startButton.onClick.RemoveAllListeners();
            if (isHostOrSolo)
            {
                var label = startButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "START";

                startButton.interactable = true;

                startButton.onClick.AddListener(OnStartClick);
            }
        }

        if (readyButton != null)
        {
            bool isMember = inParty && !party.IsLocalHost;
            readyButton.gameObject.SetActive(isMember);
            readyButton.onClick.RemoveAllListeners();
            if (isMember)
            {
                bool ready = IsLocalMemberReady(party);
                var label = readyButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = ready ? "UNREADY" : "READY";
                if (readyButton.image != null)
                    readyButton.image.sprite = ready && readyActiveSprite != null
                        ? readyActiveSprite
                        : _readyDefaultSprite;
                readyButton.interactable = true;
                bool next = !ready;
                readyButton.onClick.AddListener(() => PartyService.SetReady(next));
            }
        }
    }

    // Executes is local member ready operation.
    private bool IsLocalMemberReady(PartyLobby party)
    {
        var runner = PhotonManager.Instance != null ? PhotonManager.Instance.Runner : null;
        if (runner == null) return false;
        for (int i = 0; i < PartyLobby.MaxMembers; i++)
        {
            var m = party.Members[i];
            if (m.IsOccupied && m.Player == runner.LocalPlayer) return m.Ready;
        }
        return false;
    }

    // Executes on start click operation.
    private void OnStartClick()
    {
        var party = PartyLobby.Local;
        if (party != null && party.IsLocalHost)
        {
            Debug.Log($"[PartyPanel] Start clicked: config={selectedConfigId}, scene='{selectedSceneName}', " +
                      $"state={party.State}, members={party.MemberCount}, ready={party.ReadyCount}, " +
                      $"pendingInvites={party.PendingInviteCount}.");

            if (!party.CanStartDungeon)
            {
                string message;
                if (party.State != PartyLobby.PartyState.Lobby)
                    message = "The dungeon is already starting.";
                else if (party.MemberCount < 1)
                    message = "No party member is available to start the dungeon.";
                else if (party.ReadyCount < party.MemberCount)
                    message = "All party members must be ready before starting.";
                else
                    message = "The party cannot start this dungeon right now.";

                UIPopupBox.Notify(transform, "Notice", message);
                return;
            }
            PartyService.StartDungeon(selectedConfigId, selectedSceneName);
            return;
        }

        Close();
        DungeonManager.Instance.StartDungeon(selectedConfigId, selectedSceneName, energyCost, selectedDungeonName);
    }


    // Executes on exit click operation.
    private void OnExitClick()
    {
        var party = PartyLobby.Local;

        if (party != null)
        {
            bool wasHost = party.IsLocalHost;
            int otherMembers = Mathf.Max(0, party.MemberCount - 1);

            PartyService.LeaveParty();

            if (wasHost)
            {
                if (otherMembers > 0)
                {
                    UIPopupBox.Notify(transform, "Party", "You disbanded the party.");
                }
            }
            else
            {
                UIPopupBox.Notify(transform, "Notice", "Dungeon expedition cancelled.");
            }
        }
        Close();
    }

    // Executes close operation.
    public void Close()
    {
        if (UIManager.Instance != null && UIManager.Instance.dungeonPanel == gameObject)
        {
            UIManager.Instance.ClosePanel(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }

        if (friendModalObj != null)
        {
            Destroy(friendModalObj);
            friendModalObj = null;
        }
        if (dynamicCanvasObj != null)
        {
            Destroy(dynamicCanvasObj);
            dynamicCanvasObj = null;
        }
    }


    // Executes open friend list modal operation.
    private void OpenFriendListModal()
    {
        ClearInviteCooldownViews();
        if (friendModalObj != null) Destroy(friendModalObj);

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        friendModalObj = new GameObject("FriendListModal_Container");
        friendModalObj.transform.SetParent(canvas.transform, false);

        GameObject blockObj = new GameObject("BlockOverlay", typeof(RectTransform), typeof(Image));
        blockObj.transform.SetParent(friendModalObj.transform, false);
        blockObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);
        RectTransform blockRt = blockObj.GetComponent<RectTransform>();
        blockRt.anchorMin = Vector2.zero;
        blockRt.anchorMax = Vector2.one;
        blockRt.sizeDelta = Vector2.zero;

        GameObject frameObj = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frameObj.transform.SetParent(friendModalObj.transform, false);
        frameObj.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 0.98f);
        RectTransform frameRt = frameObj.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0.5f, 0.5f);
        frameRt.anchorMax = new Vector2(0.5f, 0.5f);
        frameRt.sizeDelta = new Vector2(340, 400);

        GameObject modalTitleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        modalTitleObj.transform.SetParent(frameObj.transform, false);
        TextMeshProUGUI modalTitle = modalTitleObj.GetComponent<TextMeshProUGUI>();
        modalTitle.text = "INVITE FRIENDS";
        modalTitle.fontSize = 16;
        modalTitle.fontStyle = FontStyles.Bold;
        modalTitle.alignment = TextAlignmentOptions.Center;
        modalTitle.color = Color.white;
        RectTransform mtRt = modalTitleObj.GetComponent<RectTransform>();
        mtRt.anchorMin = new Vector2(0.5f, 1f);
        mtRt.anchorMax = new Vector2(0.5f, 1f);
        mtRt.pivot = new Vector2(0.5f, 1f);
        mtRt.anchoredPosition = new Vector2(0, -15);
        mtRt.sizeDelta = new Vector2(300, 30);

        GameObject scrollAreaObj = new GameObject("ScrollArea", typeof(RectTransform), typeof(VerticalLayoutGroup));
        scrollAreaObj.transform.SetParent(frameObj.transform, false);
        RectTransform saRt = scrollAreaObj.GetComponent<RectTransform>();
        saRt.anchorMin = new Vector2(0, 0);
        saRt.anchorMax = new Vector2(1, 1);
        saRt.offsetMin = new Vector2(15, 65);
        saRt.offsetMax = new Vector2(-15, -45);

        VerticalLayoutGroup saLayout = scrollAreaObj.GetComponent<VerticalLayoutGroup>();
        saLayout.spacing = 8;
        saLayout.childAlignment = TextAnchor.UpperCenter;
        saLayout.childControlHeight = false;
        saLayout.childControlWidth = true;
        saLayout.childForceExpandHeight = false;

        GameObject closeBtnObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtnObj.transform.SetParent(frameObj.transform, false);
        closeBtnObj.GetComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f);
        AddHoverEffect(closeBtnObj);
        RectTransform cRt = closeBtnObj.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0f);
        cRt.anchorMax = new Vector2(0.5f, 0f);
        cRt.pivot = new Vector2(0.5f, 0f);
        cRt.anchoredPosition = new Vector2(0, 15);
        cRt.sizeDelta = new Vector2(120, 32);

        Button closeBtn = closeBtnObj.GetComponent<Button>();
        closeBtn.onClick.AddListener(() =>
        {
            Destroy(friendModalObj);
            friendModalObj = null;
            ClearInviteCooldownViews();
        });

        GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        TextMeshProUGUI closeTxt = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTxt.text = "CLOSE";
        closeTxt.fontSize = 12;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
        RectTransform ctRt = closeTxtObj.GetComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.sizeDelta = Vector2.zero;

        FriendApi.GetFriendList(
            response =>
            {
                int shown = 0;
                if (response != null)
                {
                    foreach (var friend in response)
                    {
                        if (friend == null ||
                            !friend.IsOnline ||
                            PlayerPresence.Find(friend.FriendProfileId) == null)
                            continue;
                        AddFriendRow(scrollAreaObj.transform, friend);
                        shown++;
                    }
                }

                if (shown == 0)
                {
                    AddNoFriendsLabel(scrollAreaObj.transform);
                }
            },
            error =>
            {
                Debug.LogWarning($"[PartyPanel] GetFriendList failed: {error.Message}");
                AddNoFriendsLabel(scrollAreaObj.transform);
            }
        );
    }

    // Executes add friend row operation.
    private void AddFriendRow(Transform parent, FriendDto friend)
    {
        GameObject row = new GameObject($"FriendRow_{friend.FriendName}", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 0.6f);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 45);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI txt = textObj.GetComponent<TextMeshProUGUI>();
        txt.text = $"{friend.FriendName} (Lv.{friend.FriendLevel} {friend.Class})";
        txt.fontSize = 12;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10, 0);
        rt.offsetMax = new Vector2(-75, 0);

        GameObject inviteBtnObj = new GameObject("InviteBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        inviteBtnObj.transform.SetParent(row.transform, false);
        Image btnImg = inviteBtnObj.GetComponent<Image>();
        Button btn = inviteBtnObj.GetComponent<Button>();

        RectTransform btnRt = inviteBtnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1, 0.5f);
        btnRt.anchorMax = new Vector2(1, 0.5f);
        btnRt.pivot = new Vector2(1, 0.5f);
        btnRt.anchoredPosition = new Vector2(-8, 0);
        btnRt.sizeDelta = new Vector2(60, 28);

        var livePresence = PlayerPresence.Find(friend.FriendProfileId);
        bool isInDungeon = friend.IsInDungeon ||
                           (livePresence != null && (bool)livePresence.IsInDungeon);
        bool alreadyInParty = IsProfileInParty(friend.FriendProfileId);
        bool inviteBlocked = isInDungeon || alreadyInParty;

        btnImg.color = inviteBlocked
            ? new Color(0.35f, 0.35f, 0.35f, 0.65f)
            : new Color(0.2f, 0.5f, 0.2f);
        btn.interactable = !inviteBlocked;
        if (!inviteBlocked)
            AddHoverEffect(inviteBtnObj);

        GameObject btnTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTxtObj.transform.SetParent(inviteBtnObj.transform, false);
        TextMeshProUGUI btnTxt = btnTxtObj.GetComponent<TextMeshProUGUI>();
        btnTxt.text = isInDungeon ? "IN DUN" : alreadyInParty ? "IN" : "INVITE";
        btnTxt.fontSize = 11;
        btnTxt.fontStyle = FontStyles.Bold;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = Color.white;
        RectTransform btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.sizeDelta = Vector2.zero;

        InviteCooldownView cooldownView = null;
        if (!inviteBlocked)
            cooldownView = RegisterInviteCooldownView(btn, btnTxt, btnImg);

        if (inviteBlocked)
            return;

        int profileId = friend.FriendProfileId;
        string friendName = friend.FriendName;
        btn.onClick.AddListener(() =>
        {
            if (PartyService.InviteCooldownRemaining > 0f)
            {
                StartInviteCooldownCountdown();
                UpdateInviteCooldownViews();
                return;
            }

            var presence = PlayerPresence.Find(profileId);
            if (presence != null &&
                !MapProgressionRules.CanInviteToMap(selectedMapId, presence.HighestUnlockedMapId))
            {
                UIPopupBox.Notify(transform, "Notice",
                    $"Cannot invite {friendName}. They have not unlocked {MapProgressionRules.GetDisplayName(selectedMapId)} yet.");
                return;
            }

            var result = PartyService.InviteByProfileId(profileId, selectedMapId);
            if (result == PartyService.InviteResult.Sent)
            {
                if (cooldownView != null) cooldownView.IsSent = true;
                StartInviteCooldownCountdown();
                UpdateInviteCooldownViews();
                UIPopupBox.Notify(transform, "Notice", $"Invited {friendName}.");
                btnTxt.text = "SENT";
                btnImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                btn.interactable = false;
                UpdateUI();
                return;
            }

            if (result == PartyService.InviteResult.Cooldown)
            {
                StartInviteCooldownCountdown();
                UpdateInviteCooldownViews();
            }

            if (result == PartyService.InviteResult.FriendInDungeon)
            {
                btnTxt.text = "IN DUN";
                btnImg.color = new Color(0.35f, 0.35f, 0.35f, 0.65f);
                btn.interactable = false;
            }

            UIPopupBox.Notify(transform, "Notice", result switch
            {
                PartyService.InviteResult.FriendOffline => $"{friendName} is not online right now.",
                PartyService.InviteResult.FriendInDungeon => $"{friendName} is already in a dungeon.",
                PartyService.InviteResult.Cooldown => $"Please wait {Mathf.Max(1, Mathf.CeilToInt(PartyService.InviteCooldownRemaining))}s before inviting again.",
                PartyService.InviteResult.PartyFull => "Your party is already full.",
                PartyService.InviteResult.PartyUnavailable => "Could not create the party. Try again.",
                PartyService.InviteResult.MapLocked =>
                    $"Cannot invite {friendName}. They have not unlocked {MapProgressionRules.GetDisplayName(selectedMapId)} yet.",
                _ => "You are not connected to the party service.",
            });
        });
    }

    private InviteCooldownView RegisterInviteCooldownView(Button button, TMP_Text label, Image image)
    {
        var view = new InviteCooldownView
        {
            Button = button,
            Label = label,
            Image = image,
            IsSent = false
        };

        _inviteCooldownViews.Add(view);
        UpdateInviteCooldownViews();

        if (PartyService.InviteCooldownRemaining > 0f)
            StartInviteCooldownCountdown();

        return view;
    }

    private void StartInviteCooldownCountdown()
    {
        if (_inviteCooldownCoroutine == null && isActiveAndEnabled)
            _inviteCooldownCoroutine = StartCoroutine(InviteCooldownCountdown());
    }

    private IEnumerator InviteCooldownCountdown()
    {
        var wait = new WaitForSecondsRealtime(0.1f);
        while (PartyService.InviteCooldownRemaining > 0f)
        {
            UpdateInviteCooldownViews();
            yield return wait;
        }

        UpdateInviteCooldownViews();
        _inviteCooldownCoroutine = null;
    }

    private void UpdateInviteCooldownViews()
    {
        float remaining = PartyService.InviteCooldownRemaining;
        bool coolingDown = remaining > 0f;
        string countdown = $"{Mathf.Max(1, Mathf.CeilToInt(remaining))}s";

        for (int i = _inviteCooldownViews.Count - 1; i >= 0; i--)
        {
            var view = _inviteCooldownViews[i];
            if (view == null || view.Button == null)
            {
                _inviteCooldownViews.RemoveAt(i);
                continue;
            }

            if (view.IsSent)
            {
                view.Button.interactable = false;
                if (view.Label != null) view.Label.text = "SENT";
                if (view.Image != null) view.Image.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                continue;
            }

            view.Button.interactable = !coolingDown;
            if (view.Label != null) view.Label.text = coolingDown ? countdown : "INVITE";
            if (view.Image != null)
            {
                view.Image.color = coolingDown
                    ? new Color(0.35f, 0.35f, 0.35f, 0.65f)
                    : new Color(0.2f, 0.5f, 0.2f);
            }
        }
    }

    private void ClearInviteCooldownViews()
    {
        if (_inviteCooldownCoroutine != null)
        {
            StopCoroutine(_inviteCooldownCoroutine);
            _inviteCooldownCoroutine = null;
        }

        _inviteCooldownViews.Clear();
    }

    // Executes is profile in party operation.
    // Evaluates conditions and returns a boolean result.
    private static bool IsProfileInParty(int profileId)
    {
        var party = PartyLobby.Local;
        if (party == null) return false;
        for (int i = 0; i < PartyLobby.MaxMembers; i++)
        {
            var m = party.Members[i];
            if (m.IsOccupied && m.ProfileId == profileId) return true;
        }
        return false;
    }

    // Executes add no friends label operation.
    private void AddNoFriendsLabel(Transform parent)
    {
        GameObject txtObj = new GameObject("NoFriendsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(parent, false);
        TextMeshProUGUI txt = txtObj.GetComponent<TextMeshProUGUI>();
        txt.text = "No friends online.";
        txt.fontSize = 13;
        txt.color = new Color(0.6f, 0.6f, 0.6f);
        txt.alignment = TextAlignmentOptions.Center;
        txtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 40);
    }

    // Executes add hover effect operation.
    private void AddHoverEffect(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<UIHoverScaleEffect>() == null)
        {
            go.AddComponent<UIHoverScaleEffect>();
        }
    }
}
