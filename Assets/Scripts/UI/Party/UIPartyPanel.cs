using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;

/// <summary>
/// The pre-dungeon party panel (VIEW). It renders the live party roster from
/// <see cref="PartyLobby.Local"/> and translates user clicks into <see cref="PartyService"/>
/// calls — it holds NO party business logic and NO local roster state; the authoritative
/// state is the replicated PartyLobby.
///
/// Two modes share the same UI:
///   • Solo  — no party yet. Slot 1 shows the local player; slots 2-4 show "+" invite
///             buttons; Start enters the dungeon single-player (existing flow, untouched).
///   • Party — the local player created/joined a party. Slots show real members
///             (name / class / level / ready / host icon) and update in realtime via
///             Fusion; Start/Ready/Kick/Leave route through PartyService.
///
/// Inviting the first friend lazily creates the party (PartyService.InviteByProfileId),
/// so the solo→party transition is seamless.
/// </summary>
public class UIPartyPanel : MonoBehaviour
{
    public static UIPartyPanel Instance { get; private set; }

    [Header("Static References")]
    [SerializeField] private TMP_Text dungeonNameText;
    [SerializeField] private TMP_Text energyCostText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button readyButton;   // BottomSection/ReadyButton (member only)
    [SerializeField] private Button inviteButton;  // BottomSection/InviteButton (global → friend list)
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
    [SerializeField] private GameObject dropItemPrefab; // Optional prefab to instantiate for drop items

    [Header("Runtime Info")]
    private int selectedConfigId = 1;
    private string selectedSceneName = "AbandonedMines";
    // Không có tên mặc định: một cái tên cứng ở đây sẽ hiện ĐÚNG như thật cho dungeon sai
    // (mọi cửa từng mặc định "Abandoned Mines", kể cả configId 2 = Dragon's Lair).
    // Rỗng thì UpdateUI() hiện "Dungeon" — trung tính, không khẳng định điều gì sai.
    private string selectedDungeonName;
    private string selectedDescription = "";
    private System.Collections.Generic.List<ChestItemResponse> possibleDrops = new();
    private int energyCost = 20;
    private int playerEnergy = 0;
    private int goldMinReward = 0;
    private int goldMaxReward = 0;
    private int experienceReward = 0;

    // Local player identity (for the host slot in solo mode + as fallback).
    private string localPlayerName = "Player";
    private int localPlayerLevel = 1;

    private GameObject dynamicCanvasObj;
    private GameObject friendModalObj;

    // Currently hooked party (for instance-event subscription lifecycle).
    private PartyLobby _hookedParty;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        PartyLobby.OnLocalPartyChanged += HandleLocalPartyChanged;
        RehookPartyEvents();
    }

    private void OnDisable()
    {
        PartyLobby.OnLocalPartyChanged -= HandleLocalPartyChanged;
        UnhookPartyEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Party event wiring — keep instance-level subscriptions pointed at the
    // current PartyLobby.Local (which changes as we join / leave / get disbanded).
    // ─────────────────────────────────────────────────────────────────────────

    private void RehookPartyEvents()
    {
        UnhookPartyEvents();
        _hookedParty = PartyLobby.Local;
        if (_hookedParty != null)
        {
            _hookedParty.OnRosterChanged += UpdateUI;
            _hookedParty.OnPartyStateChanged += HandlePartyState;
        }
    }

    private void UnhookPartyEvents()
    {
        if (_hookedParty != null)
        {
            _hookedParty.OnRosterChanged -= UpdateUI;
            _hookedParty.OnPartyStateChanged -= HandlePartyState;
            _hookedParty = null;
        }
    }

    private void HandleLocalPartyChanged()
    {
        RehookPartyEvents();
        // The party is created lazily on the first invite — AFTER the host opened the
        // panel (when PartyLobby.Local was still null, so the initial publish was a
        // no-op). Republish now that we're seated as host so members' panels get the
        // real dungeon config/name/scene instead of the empty networked defaults.
        PublishDungeonSelectionIfHost();
        if (gameObject.activeInHierarchy) UpdateUI();
    }

    private void HandlePartyState(PartyLobby.PartyState state)
    {
        // Leaving the lobby (host pressed Start) closes this panel; the actual dungeon
        // transition is driven by PartyManager (Step 5).
        if (state != PartyLobby.PartyState.Lobby)
        {
            Close();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Open / data load
    // ─────────────────────────────────────────────────────────────────────────

    // Direct initialization from DungeonEntrance triggers
    public void OpenForDungeon(int configId, string sceneName, int cost, string displayName)
    {
        selectedConfigId = configId;
        selectedSceneName = sceneName;
        energyCost = cost;
        selectedDungeonName = displayName;

        // A member may open the panel BEFORE the host has published a real config id
        // (arrives via PartyLobby networked props a frame later). configId <= 0 would
        // hit DungeonApi.GetById(0) → 404, so skip the fetch and just show the panel;
        // UpdateUI reads the host's published DungeonName once it replicates.
        if (configId <= 0)
        {
            gameObject.SetActive(true);
            FetchPlayerEnergy();
            return;
        }

        // Fetch detailed config info via GetById to grab energy cost, description and drops.
        // Level requirement KHÔNG đọc ở đây: DungeonEntrance tự fetch ngưỡng của chính nó
        // và là nơi duy nhất gate, nên panel giữ thêm một bản copy chỉ tạo cơ hội lệch.
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

    /// <summary>If the local player hosts a party, share the current selection so
    /// every member's panel shows the same dungeon (24.2).</summary>
    private void PublishDungeonSelectionIfHost()
    {
        if (PartyService.IsHost)
            PartyService.SetDungeon(selectedConfigId, selectedSceneName, selectedDungeonName);
    }

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
                Debug.LogWarning($"[UIPartyPanel] GetMyProfile failed: {error.Message}");
                playerEnergy = 100; // Fallback
                localPlayerName = WorldState.PlayerName ?? "Player";
                localPlayerLevel = Mathf.Max(1, WorldState.PlayerLevel);
                UpdateUI();
            }
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rendering
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (transform.Find("Players") == null)
        {
            Debug.LogError("[UIPartyPanel] Players child not found on PartyPanel! Designed UI hierarchy is incorrect.");
            return;
        }

        FindReferences();

        var party = PartyLobby.Local;
        bool nonHostMember = party != null && !party.IsLocalHost;

        // Header dungeon name: a non-host member reads the host's published selection.
        string dungeonName = selectedDungeonName;
        if (nonHostMember && !string.IsNullOrEmpty(party.DungeonName.Value))
            dungeonName = party.DungeonName.Value;

        if (dungeonNameText != null)
        {
            // Tên chưa về (hoặc server trả rỗng): nhãn trung tính, KHÔNG đoán tên dungeon.
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

    private void UpdateDungeonInfoPanel()
    {
        if (descriptionText != null)
        {
            descriptionText.text = selectedDescription;
        }

        if (dropsContainer != null)
        {
            // Clear existing
            foreach (Transform child in dropsContainer)
            {
                Destroy(child.gameObject);
            }

            if (true) // Just to keep block indentation
            {
                // Spawn Gold if available
                if (goldMaxReward > 0)
                {
                    SpawnDropItem("Gold", goldMinReward, goldMaxReward, null);
                }

                // Spawn Exp if available
                if (experienceReward > 0)
                {
                    SpawnDropItem("Experience", experienceReward, experienceReward, null);
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
    }

    private void SpawnDropItem(string itemName, int minQty, int maxQty, string iconUrl)
    {
        GameObject itemObj;
        if (dropItemPrefab != null)
        {
            itemObj = Instantiate(dropItemPrefab, dropsContainer);
        }
        else
        {
            itemObj = new GameObject("DropItem", typeof(RectTransform), typeof(Image));
            itemObj.transform.SetParent(dropsContainer, false);
            var rt = itemObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40, 40);
        }

        // Try to load icon
        Image image = null;
        var iconTransform = itemObj.transform.Find("Icon");
        if (iconTransform != null)
        {
            image = iconTransform.GetComponent<Image>();
        }
        else
        {
            image = itemObj.GetComponentInChildren<Image>();
        }
        
        if (image != null && !string.IsNullOrEmpty(itemName))
        {
            Sprite sprite = null;
            if (ItemIconDatabase.Instance != null)
            {
                sprite = ItemIconDatabase.Instance.GetIcon(itemName, null);
            }
            
            if (sprite == null)
            {
                sprite = Resources.Load<Sprite>(iconUrl) ?? Resources.Load<Sprite>("Icons/Items/" + itemName);
            }

            if (sprite != null)
            {
                image.sprite = sprite;
                image.enabled = true;
                image.preserveAspect = true;
            }
            else
            {
                Debug.LogWarning($"[UIPartyPanel] Không tìm thấy hình ảnh cho item: {itemName}");
            }
        }

        // Update quantity text if exists
        var qtyText = itemObj.GetComponentInChildren<TMPro.TMP_Text>();
        if (qtyText != null)
        {
            if (minQty == maxQty)
                qtyText.text = $"x{maxQty}";
            else
                qtyText.text = $"x{minQty}-{maxQty}";
        }
    }

    private void UpdateEnergyCostLabel()
    {
        if (energyCostText == null) return;
        energyCostText.text = $"-{energyCost}";
        energyCostText.color = playerEnergy >= energyCost
            ? new Color(0.18f, 0.8f, 0.25f)   // green
            : new Color(0.9f, 0.2f, 0.2f);    // red
    }

    private void FindReferences()
    {
        // 1. Header & Exit button
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

        // 2. BottomSection — StartButton, ReadyButton, InviteButton (global), EnergyCost.
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

        // 3. Auto-find Description and Drops if null or wrong
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

        if (dropsContainer != null && dropsContainer.name == "RewardItem")
        {
            dropsContainer = dropsContainer.parent;
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

        if (dropsContainer != null && dropsContainer.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
        {
            var hg = dropsContainer.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hg.childControlHeight = false;
            hg.childControlWidth = false;
            hg.childForceExpandHeight = false;
            hg.childForceExpandWidth = false;
            hg.childAlignment = TextAnchor.MiddleCenter;
            hg.spacing = 10;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Player slots (24.1 View Party List)
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdatePlayersPanel()
    {
        Transform playersTrans = transform.Find("Players");
        if (playersTrans == null) return;

        EnsureSlotsResolved();
        if (slots == null || slots.Length == 0) return;

        var party = PartyLobby.Local;
        bool localIsHost = party != null && party.IsLocalHost;

        // ── Slot 0: host ─────────────────────────────────────────────────────
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
            slots[0].RenderHost(hostName, hostLevel, hostCls, FlagFor(hostCls), NameplateFor(hostCls), SkinPortraitFor(hostSkin));
        }

        // ── Slots 1..N: other members / invite buttons ───────────────────────
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
                    skinPortrait: SkinPortraitFor(m.SkinId));
            }
            else
            {
                slot.RenderEmpty();
            }
        }
    }

    /// <summary>
    /// Confirms a kick through <c>Canvas/PopupLayer/UIPopup</c> in the Main Scene.
    /// This used to call <c>UIPopupManager</c>, which was the wrong popup: that is the shared
    /// generic dialog, not the party-specific one the designers built.
    /// </summary>
    private void ConfirmKick(string memberName, Action onConfirm)
    {
        UIPopupBox.Show(transform, "Party", $"Kick {memberName} from the party?", onConfirm);
    }

    private void EnsureSlotsResolved()
    {
        if (slots != null && slots.Length > 0)
        {
            // Fill any null entries by name (Player1..PlayerN under Players).
            bool anyNull = false;
            for (int i = 0; i < slots.Length; i++) if (slots[i] == null) anyNull = true;
            if (!anyNull) { EnsureAvatarDb(); return; }
        }

        Transform playersTrans = transform.Find("Players");
        if (playersTrans != null)
        {
            // Only the "Player*" podium children are real slots — a decorative child
            // (e.g. a header Image) under Players has no UIPartySlot and is skipped.
            // GetChild order == sibling order, so slots stay Player1..PlayerN.
            var resolved = new System.Collections.Generic.List<UIPartySlot>();
            for (int i = 0; i < playersTrans.childCount; i++)
            {
                var child = playersTrans.GetChild(i);
                if (child == null || !child.name.StartsWith("Player")) continue;
                var comp = child.GetComponent<UIPartySlot>();
                if (comp == null) comp = child.gameObject.AddComponent<UIPartySlot>();
                resolved.Add(comp);

                // Auto-scale down the huge PlayerHUD template
                var rt = child.GetComponent<RectTransform>();
                if (rt != null) rt.localScale = new Vector3(0.6f, 0.6f, 1f);
            }
            slots = resolved.ToArray();

            // Auto-configure layout for the Players container
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
                // Position it below the main PlayerHUD
                playersRt.anchoredPosition = new Vector2(20, -150); 
            }
        }

        EnsureAvatarDb();
    }

    private void EnsureAvatarDb()
    {
        if (avatarDatabase == null)
            avatarDatabase = ClassAvatarDatabaseSO.LoadDefault();
    }

    private Sprite FlagFor(CharacterClass cls)
    {
        return avatarDatabase != null ? avatarDatabase.GetFlag(cls) : null;
    }

    private Sprite NameplateFor(CharacterClass cls)
    {
        return avatarDatabase != null ? avatarDatabase.GetNameplate(cls) : null;
    }

    /// <summary>Portrait for a member's equipped skin — the same preview sprite the
    /// inventory's skin tab renders, so a slot matches what that player is wearing.</summary>
    private Sprite SkinPortraitFor(int skinId)
    {
        if (skinId <= 0) return null;
        if (skinDatabase == null) skinDatabase = SkinDatabaseSO.LoadDefault();
        return skinDatabase != null ? skinDatabase.GetPreviewSprite(skinId) : null;
    }

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

    // ─────────────────────────────────────────────────────────────────────────
    // Bottom bar: Start (host / solo) or Ready toggle (member) — 24.8 + Start
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateBottomBar()
    {
        var party = PartyLobby.Local;
        bool inParty = party != null;
        bool isHostOrSolo = party == null || party.IsLocalHost;

        // Invite is available whenever the local player can host (solo or party host).
        if (inviteButton != null)
        {
            inviteButton.gameObject.SetActive(isHostOrSolo);
            inviteButton.interactable = isHostOrSolo;
        }

        // ── Start button: host or solo only ──────────────────────────────────
        if (startButton != null)
        {
            startButton.gameObject.SetActive(isHostOrSolo);
            startButton.onClick.RemoveAllListeners();
            if (isHostOrSolo)
            {
                var label = startButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "START";

                // Năng lượng KHÔNG chặn ở đây nữa — chỉ kiểm tra khi mở rương trong dungeon
                // (backend trừ energy ở claim-reward, BR-10). Nhãn EnergyCost vẫn tô đỏ để
                // báo trước là sẽ không nhận được thưởng nếu không kịp hồi năng lượng.
                if (inParty)
                    startButton.interactable = party.CanStartDungeon;
                else
                    startButton.interactable = true;

                startButton.onClick.AddListener(OnStartClick);
            }
        }

        // ── Ready button: non-host member only ───────────────────────────────
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
                readyButton.interactable = true;
                bool next = !ready;
                readyButton.onClick.AddListener(() => PartyService.SetReady(next));
            }
        }
    }

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

    private void OnStartClick()
    {
        var party = PartyLobby.Local;
        if (party != null && party.IsLocalHost)
        {
            // Party path — flip networked state; PartyManager (Step 5) drives the load.
            if (!party.CanStartDungeon)
            {
                UIPopupBox.Notify(transform, "Notice", "All members must be ready (need at least 2 players).");
                return;
            }
            PartyService.StartDungeon(selectedConfigId, selectedSceneName);
            return;
        }

        // Solo path — unchanged existing single-player dungeon entry.
        Close();
        DungeonManager.Instance.StartDungeon(selectedConfigId, selectedSceneName, energyCost, selectedDungeonName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Exit / leave (24.7)
    // ─────────────────────────────────────────────────────────────────────────

    private void OnExitClick()
    {
        // Leaving the panel while in a party leaves the party too.
        if (PartyLobby.Local != null)
        {
            PartyService.LeaveParty();
        }
        // Notify BEFORE Close(): the popup is located via GetComponentInParent<Canvas>() from this
        // transform, and that skips inactive objects — after Close() this panel is deactivated, the
        // Canvas lookup returns null, and the message degrades to a warning with nothing shown.
        UIPopupBox.Notify(transform, "Notice", "Dungeon expedition cancelled.");
        Close();
    }

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

    // ─────────────────────────────────────────────────────────────────────────
    // Friend list modal (24.4 Invite Player)
    // ─────────────────────────────────────────────────────────────────────────

    private void OpenFriendListModal()
    {
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

        // FriendApi (/api/friend), NOT PlayerApi.GetFriends: the latter returns
        // PlayerProfileResponse, which carries no online flag at all — so every friend
        // was listed and an offline one only failed later, at PartyService.InviteByProfileId
        // (FriendOffline). FriendDto has IsOnline, and an invite can only reach a friend
        // who is present in the social lobby, so offline rows are filtered out here.
        FriendApi.GetFriendList(
            response =>
            {
                int shown = 0;
                if (response != null)
                {
                    foreach (var friend in response)
                    {
                        if (friend == null || !friend.IsOnline) continue;
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
                Debug.LogWarning($"[UIPartyPanel] GetFriendList failed: {error.Message}");
                AddNoFriendsLabel(scrollAreaObj.transform);
            }
        );
    }

    private void AddFriendRow(Transform parent, FriendDto friend)
    {
        GameObject row = new GameObject($"FriendRow_{friend.FriendName}", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 0.6f);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 45);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI txt = textObj.GetComponent<TextMeshProUGUI>();
        txt.text = $"👤 {friend.FriendName} (Lv.{friend.FriendLevel} {friend.Class})";
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
        AddHoverEffect(inviteBtnObj);

        RectTransform btnRt = inviteBtnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1, 0.5f);
        btnRt.anchorMax = new Vector2(1, 0.5f);
        btnRt.pivot = new Vector2(1, 0.5f);
        btnRt.anchoredPosition = new Vector2(-8, 0);
        btnRt.sizeDelta = new Vector2(60, 28);

        // Already in the party?
        bool alreadyInParty = IsProfileInParty(friend.FriendProfileId);
        btnImg.color = alreadyInParty ? new Color(0.4f, 0.4f, 0.4f, 0.5f) : new Color(0.2f, 0.5f, 0.2f);
        btn.interactable = !alreadyInParty;

        GameObject btnTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTxtObj.transform.SetParent(inviteBtnObj.transform, false);
        TextMeshProUGUI btnTxt = btnTxtObj.GetComponent<TextMeshProUGUI>();
        btnTxt.text = alreadyInParty ? "IN" : "INVITE";
        btnTxt.fontSize = 11;
        btnTxt.fontStyle = FontStyles.Bold;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = Color.white;
        RectTransform btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.sizeDelta = Vector2.zero;

        if (!alreadyInParty)
        {
            int profileId = friend.FriendProfileId;
            string friendName = friend.FriendName;
            btn.onClick.AddListener(() =>
            {
                var result = PartyService.InviteByProfileId(profileId);
                if (result == PartyService.InviteResult.Sent)
                {
                    UIPopupBox.Notify(transform, "Notice", $"Invited {friendName}.");
                    btnTxt.text = "SENT";
                    btnImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                    btn.interactable = false;
                    UpdateUI();
                }
                else
                {
                    // Report the real reason: blaming the friend for a local connection
                    // problem sent players hunting a bug on the wrong side.
                    UIPopupBox.Notify(transform, "Notice", result switch
                    {
                        PartyService.InviteResult.FriendOffline => $"{friendName} is not online right now.",
                        PartyService.InviteResult.PartyFull => "Your party is already full.",
                        PartyService.InviteResult.PartyUnavailable => "Could not create the party. Try again.",
                        _ => "You are not connected to the party service.",
                    });
                }
            });
        }
    }

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

    private void AddHoverEffect(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<UIHoverScaleEffect>() == null)
        {
            go.AddComponent<UIHoverScaleEffect>();
        }
    }
}
