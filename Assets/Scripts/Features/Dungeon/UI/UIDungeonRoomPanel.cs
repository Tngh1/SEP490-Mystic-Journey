using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public class UIDungeonRoomPanel : MonoBehaviour
{
    public static UIDungeonRoomPanel Instance { get; private set; }

    [Header("Static References")]
    [SerializeField] private TMP_Text dungeonNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text energyCostText;
    [SerializeField] private TMP_Text playerEnergyText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button closeButton;

    [Header("Runtime Info")]
    private int selectedConfigId = 1;
    private string selectedSceneName = "AbandonedMines";
    private string selectedDungeonName = "Abandoned Mines";
    private int energyCost = 20;
    private int playerEnergy = 0;
    private int recommendedLevel = 1;
    private int difficulty = 1;

    // Party management
    private string hostPlayerName = "Player";
    private readonly List<string> invitedFriends = new List<string>();

    private GameObject dynamicCanvasObj;
    private GameObject friendModalObj;

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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Direct initialization from DungeonEntrance triggers
    public void OpenForDungeon(int configId, string sceneName, int cost, string displayName)
    {
        selectedConfigId = configId;
        selectedSceneName = sceneName;
        energyCost = cost;
        selectedDungeonName = displayName;
        
        // Fetch detailed config info via GetAll to grab recommended level and difficulty
        DungeonApi.Instance.GetById(configId,
            response =>
            {
                if (response != null)
                {
                    recommendedLevel = response.LevelRequirement;
                    difficulty = response.Difficulty;
                }
                invitedFriends.Clear();
                gameObject.SetActive(true);
                FetchPlayerEnergy();
            },
            error =>
            {
                // Fallback on error
                recommendedLevel = 1;
                difficulty = 2;
                invitedFriends.Clear();
                gameObject.SetActive(true);
                FetchPlayerEnergy();
            }
        );
    }

    // Backward compatibility for general string matching search
    public void OpenForDungeon(string matchName)
    {
        gameObject.SetActive(true);
        LoadDungeonAndEnergy(matchName);
    }

    private void LoadDungeonAndEnergy(string matchName)
    {
        DungeonApi.Instance.GetAll(1, 10,
            onSuccess: response =>
            {
                if (response?.Items != null)
                {
                    DungeonResponse mines = null;
                    foreach (var d in response.Items)
                    {
                        if (d != null && d.Name != null && d.Name.ToLower().Contains(matchName.ToLower()))
                        {
                            mines = d;
                            break;
                        }
                    }

                    if (mines != null)
                    {
                        selectedConfigId = mines.DungeonConfigId;
                        selectedDungeonName = mines.Name;
                        energyCost = mines.EnergyCost;
                        recommendedLevel = mines.LevelRequirement;
                        difficulty = mines.Difficulty;
                        selectedSceneName = "AbandonedMines";
                        Debug.Log($"[UIDungeonRoomPanel] Found dungeon: {selectedDungeonName} (ConfigID={selectedConfigId}, Cost={energyCost})");
                    }
                    else
                    {
                        Debug.LogWarning($"[UIDungeonRoomPanel] Match name '{matchName}' not found in active dungeons. Using default config.");
                        selectedConfigId = 1;
                        selectedSceneName = "AbandonedMines";
                        selectedDungeonName = "Abandoned Mines";
                        energyCost = 20;
                        recommendedLevel = 1;
                        difficulty = 2;
                    }
                }
                invitedFriends.Clear();
                FetchPlayerEnergy();
            },
            onError: error =>
            {
                Debug.LogWarning($"[UIDungeonRoomPanel] GetAll dungeons failed: {error.Message}. Using default.");
                selectedConfigId = 1;
                selectedSceneName = "AbandonedMines";
                selectedDungeonName = "Abandoned Mines";
                energyCost = 20;
                recommendedLevel = 1;
                difficulty = 2;
                invitedFriends.Clear();
                FetchPlayerEnergy();
            }
        );
    }

    private void FetchPlayerEnergy()
    {
        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                playerEnergy = profile.Energy;
                hostPlayerName = profile.DisplayName;
                UpdateUI();
            },
            error =>
            {
                Debug.LogWarning($"[UIDungeonRoomPanel] GetMyProfile failed: {error.Message}");
                playerEnergy = 100; // Fallback
                hostPlayerName = "Player";
                UpdateUI();
            }
        );
    }

    private void UpdateUI()
    {
        if (transform.Find("Players") != null)
        {
            // We are attached to the designed TeamPanel!
            FindReferences();

            if (dungeonNameText != null)
            {
                dungeonNameText.text = selectedDungeonName;
                dungeonNameText.textWrappingMode = TextWrappingModes.NoWrap;
                dungeonNameText.enableAutoSizing = true;
                dungeonNameText.fontSizeMax = 48;
                dungeonNameText.fontSizeMin = 18;
            }
            if (descriptionText != null) descriptionText.text = "Enter the abandoned mines and defeat the Ogre Warlord to claim the ancient reward chest.";
            
            if (energyCostText != null)
            {
                energyCostText.text = $"-{energyCost}";
                if (playerEnergy >= energyCost)
                {
                    energyCostText.color = new Color(0.18f, 0.8f, 0.25f); // Soft Green
                }
                else
                {
                    energyCostText.color = new Color(0.9f, 0.2f, 0.2f); // Red
                }
            }

            if (startButton != null)
            {
                startButton.interactable = (playerEnergy >= energyCost);
            }
            
            UpdatePlayersPanel();
        }
        else
        {
            Debug.LogError("[UIDungeonRoomPanel] Players child object not found on TeamPanel! Designed UI hierarchy is incorrect.");
        }
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
                    closeButton.onClick.AddListener(OnCancelParty);
                    AddHoverEffect(closeButton.gameObject);
                }
            }

            Transform dungeonNameTrans = headerTrans.Find("DungeonName");
            if (dungeonNameTrans != null)
            {
                dungeonNameText = dungeonNameTrans.GetComponentInChildren<TMP_Text>(true) ?? dungeonNameTrans.GetComponentInChildren<TMP_Text>();
                if (dungeonNameText == null)
                {
                    Transform textChild = dungeonNameTrans.Find("Text (TMP)");
                    if (textChild != null) dungeonNameText = textChild.GetComponent<TMP_Text>();
                }
            }
        }

        // 2. DungeonInformation
        Transform infoTrans = transform.Find("DungeonInformation");
        if (infoTrans != null)
        {
            Transform descPanelTrans = infoTrans.Find("DescriptionPanel");
            if (descPanelTrans != null)
            {
                Transform descTrans = descPanelTrans.Find("Description");
                descriptionText = (descTrans != null ? descTrans : descPanelTrans).GetComponentInChildren<TMP_Text>(true);
            }

            SetTextOfChild(infoTrans, "TypeDungeon", $"Type: Normal");
            SetTextOfChild(infoTrans, "LevelRequirement", $"Req. Level: Lv. {recommendedLevel}");
            SetTextOfChild(infoTrans, "Difficulty", $"Difficulty: {difficulty} / 5 Stars");
        }

        // 3. BottomSection
        Transform bottomTrans = transform.Find("BottomSection");
        if (bottomTrans != null)
        {
            Transform startBtnTrans = bottomTrans.Find("StartButton");
            if (startBtnTrans != null)
            {
                startButton = startBtnTrans.GetComponent<Button>();
                if (startButton != null)
                {
                    startButton.onClick.RemoveAllListeners();
                    startButton.onClick.AddListener(OnStartClick);
                    AddHoverEffect(startButton.gameObject);
                }
            }

            Transform energyTrans = bottomTrans.Find("EnergyCost");
            if (energyTrans != null)
            {
                energyCostText = energyTrans.GetComponentInChildren<TMP_Text>(true);
                if (energyCostText == null)
                {
                    Transform textChild = energyTrans.Find("Text (TMP)");
                    if (textChild != null) energyCostText = textChild.GetComponent<TMP_Text>();
                }
            }
        }
    }

    private void SetTextOfChild(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            var tmp = child.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = text;
                return;
            }
            var legacy = child.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.text = text;
            }
        }
    }

    private void UpdatePlayersPanel()
    {
        Transform playersTrans = transform.Find("Players");
        if (playersTrans == null) return;

        // Player 1 (Host)
        Transform p1Trans = playersTrans.Find("Player1");
        if (p1Trans != null)
        {
            Transform leaderIcon = p1Trans.Find("LeaderIcon");
            if (leaderIcon != null) leaderIcon.gameObject.SetActive(true);

            SetTextOfChild(p1Trans, "Level", $"{hostPlayerName} (Lv. {playerEnergy / 10 + 1})");
        }

        // Slots 2, 3, 4
        for (int i = 0; i < 3; i++)
        {
            string slotName = $"Player{i + 2}";
            Transform pSlot = playersTrans.Find(slotName);
            if (pSlot == null) continue;

            int friendIdx = i;
            Transform inviteBtnTrans = pSlot.Find("InviteButton");
            Transform statusTrans = pSlot.Find("Status");

            if (friendIdx < invitedFriends.Count)
            {
                string friendName = invitedFriends[friendIdx];

                if (inviteBtnTrans != null)
                {
                    inviteBtnTrans.gameObject.SetActive(true);
                    AddHoverEffect(inviteBtnTrans.gameObject);
                    var btn = inviteBtnTrans.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() =>
                        {
                            invitedFriends.Remove(friendName);
                            UpdateUI();
                        });
                    }
                    var tmp = inviteBtnTrans.GetComponentInChildren<TMP_Text>(true);
                    if (tmp != null) tmp.text = $"{friendName} (Kick)";
                    else
                    {
                        var txt = inviteBtnTrans.GetComponentInChildren<Text>(true);
                        if (txt != null) txt.text = $"{friendName} (Kick)";
                    }
                }

                Transform podium = pSlot.Find("Podium");
                if (podium != null) podium.gameObject.SetActive(true);

                if (statusTrans != null)
                {
                    statusTrans.gameObject.SetActive(true);
                    Transform ready = statusTrans.Find("ReadyIcon");
                    Transform notReady = statusTrans.Find("NotReadyIcon");
                    if (ready != null) ready.gameObject.SetActive(true);
                    if (notReady != null) notReady.gameObject.SetActive(false);
                }
            }
            else
            {
                if (inviteBtnTrans != null)
                {
                    inviteBtnTrans.gameObject.SetActive(true);
                    AddHoverEffect(inviteBtnTrans.gameObject);
                    var btn = inviteBtnTrans.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => OpenFriendListModal());
                    }
                    var tmp = inviteBtnTrans.GetComponentInChildren<TMP_Text>(true);
                    if (tmp != null) tmp.text = "";
                    else
                    {
                        var txt = inviteBtnTrans.GetComponentInChildren<Text>(true);
                        if (txt != null) txt.text = "";
                    }
                }

                Transform podium = pSlot.Find("Podium");
                if (podium != null) podium.gameObject.SetActive(false);

                if (statusTrans != null)
                {
                    statusTrans.gameObject.SetActive(false);
                }
            }
        }
    }

    private void OnStartClick()
    {
        // Re-validate energy cost
        if (playerEnergy < energyCost)
        {
            WorldRuntimeEvents.RaiseMessage("Not enough energy for this dungeon!");
            return;
        }

        Close();
        
        // Pass party members list to DungeonManager
        DungeonManager.Instance.StartDungeon(selectedConfigId, selectedSceneName, energyCost, selectedDungeonName, invitedFriends);
    }

    private void OnCancelParty()
    {
        // Clear all temporary party data
        invitedFriends.Clear();
        Close();
        
        WorldRuntimeEvents.RaiseMessage("Dungeon expedition cancelled.");
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

    private void OpenFriendListModal()
    {
        if (friendModalObj != null)
        {
            Destroy(friendModalObj);
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Create Modal Container GameObject
        friendModalObj = new GameObject("FriendListModal_Container");
        friendModalObj.transform.SetParent(canvas.transform, false);

        // Blocks clicks to lobby under modal
        GameObject blockObj = new GameObject("BlockOverlay", typeof(RectTransform), typeof(Image));
        blockObj.transform.SetParent(friendModalObj.transform, false);
        blockObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);
        RectTransform blockRt = blockObj.GetComponent<RectTransform>();
        blockRt.anchorMin = Vector2.zero;
        blockRt.anchorMax = Vector2.one;
        blockRt.sizeDelta = Vector2.zero;

        // Modal Frame Panel
        GameObject frameObj = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frameObj.transform.SetParent(friendModalObj.transform, false);
        frameObj.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 0.98f);
        RectTransform frameRt = frameObj.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0.5f, 0.5f);
        frameRt.anchorMax = new Vector2(0.5f, 0.5f);
        frameRt.sizeDelta = new Vector2(340, 400);

        // Title
        GameObject modalTitleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        modalTitleObj.transform.SetParent(frameObj.transform, false);
        TextMeshProUGUI modalTitle = modalTitleObj.GetComponent<TextMeshProUGUI>();
        modalTitle.text = "FRIENDS LIST";
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

        // Scroll Area or Vertical Layout
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

        // Close Button
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

        // Call GET friends API
        PlayerApi.Instance.GetFriends(
            response =>
            {
                if (response?.Data != null && response.Data.Length > 0)
                {
                    foreach (var friend in response.Data)
                    {
                        if (friend == null) continue;
                        
                        AddFriendRow(scrollAreaObj.transform, friend);
                    }
                }
                else
                {
                    AddNoFriendsLabel(scrollAreaObj.transform);
                }
            },
            error =>
            {
                Debug.LogWarning($"[UIDungeonRoomPanel] GetFriends failed: {error.Message}");
                AddNoFriendsLabel(scrollAreaObj.transform);
            }
        );
    }

    private void AddFriendRow(Transform parent, PlayerProfileResponse friend)
    {
        GameObject row = new GameObject($"FriendRow_{friend.DisplayName}", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 0.6f);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 45);

        // Info text
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI txt = textObj.GetComponent<TextMeshProUGUI>();
        txt.text = $"👤 {friend.DisplayName} (Lv.{friend.Level} {friend.PlayerClass})";
        txt.fontSize = 12;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10, 0);
        rt.offsetMax = new Vector2(-75, 0);

        // Invite button
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

        bool alreadyInvited = invitedFriends.Contains(friend.DisplayName);
        if (alreadyInvited)
        {
            btnImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            btn.interactable = false;
        }
        else
        {
            btnImg.color = new Color(0.2f, 0.5f, 0.2f);
            btn.onClick.AddListener(() =>
            {
                invitedFriends.Add(friend.DisplayName);
                Destroy(friendModalObj);
                friendModalObj = null;
                UpdateUI();
            });
        }

        GameObject btnTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTxtObj.transform.SetParent(inviteBtnObj.transform, false);
        TextMeshProUGUI btnTxt = btnTxtObj.GetComponent<TextMeshProUGUI>();
        btnTxt.text = alreadyInvited ? "INVITED" : "INVITE";
        btnTxt.fontSize = 11;
        btnTxt.fontStyle = FontStyles.Bold;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = Color.white;

        RectTransform btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.sizeDelta = Vector2.zero;
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

public class UIHoverScaleEffect : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    private Vector3 originalScale;
    private Vector3 targetScale;

    private void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 12f);
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale * 1.05f;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    private void OnDisable()
    {
        transform.localScale = originalScale;
        targetScale = originalScale;
    }
}
