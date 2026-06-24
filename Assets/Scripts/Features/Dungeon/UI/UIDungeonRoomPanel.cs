using System;
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

    private GameObject dynamicCanvasObj;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void OpenForDungeon(string matchName)
    {
        gameObject.SetActive(true);
        LoadDungeonAndEnergy(matchName);
    }

    private void LoadDungeonAndEnergy(string matchName)
    {
        // 1. Fetch Dungeons list to find target dungeon config
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
                        // For scene naming convention: remove spaces or match AbandonedMines
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
                    }
                }
                
                // Fetch player energy
                FetchPlayerEnergy();
            },
            onError: error =>
            {
                Debug.LogWarning($"[UIDungeonRoomPanel] GetAll dungeons failed: {error.Message}. Using default.");
                selectedConfigId = 1;
                selectedSceneName = "AbandonedMines";
                selectedDungeonName = "Abandoned Mines";
                energyCost = 20;

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
                UpdateUI();
            },
            error =>
            {
                Debug.LogWarning($"[UIDungeonRoomPanel] GetMyProfile failed: {error.Message}");
                playerEnergy = 100; // Fallback
                UpdateUI();
            }
        );
    }

    private void UpdateUI()
    {
        // Check if inspector references are assigned, otherwise dynamically build the UI
        if (dungeonNameText == null || startButton == null)
        {
            BuildDynamicUI();
            return;
        }

        if (dungeonNameText != null) dungeonNameText.text = selectedDungeonName;
        if (descriptionText != null) descriptionText.text = "Tiến vào hầm mỏ bỏ hoang và tiêu diệt Boss Ogre để giành lấy rương báu cổ xưa.";
        if (energyCostText != null) energyCostText.text = $"Năng Lượng Tiêu Hao: {energyCost}";
        if (playerEnergyText != null) playerEnergyText.text = $"Năng Lượng Của Bạn: {playerEnergy}";

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartClick);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnStartClick()
    {
        if (playerEnergy < energyCost)
        {
            WorldRuntimeEvents.RaiseMessage("Không đủ năng lượng để thám hiểm!");
            return;
        }

        Close();
        DungeonManager.Instance.StartDungeon(selectedConfigId, selectedSceneName, energyCost, selectedDungeonName);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        if (dynamicCanvasObj != null)
        {
            Destroy(dynamicCanvasObj);
            dynamicCanvasObj = null;
        }
    }

    private void BuildDynamicUI()
    {
        if (dynamicCanvasObj != null)
        {
            Destroy(dynamicCanvasObj);
        }

        // Create UI programmatically
        dynamicCanvasObj = new GameObject("DungeonRoomPanel_DynamicCanvas");
        Canvas canvas = dynamicCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        
        dynamicCanvasObj.AddComponent<CanvasScaler>();
        dynamicCanvasObj.AddComponent<GraphicRaycaster>();

        // Background dark overlay
        GameObject bgObj = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(dynamicCanvasObj.transform, false);
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.75f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Container panel
        GameObject containerObj = new GameObject("Container", typeof(RectTransform), typeof(Image));
        containerObj.transform.SetParent(bgObj.transform, false);
        Image containerImg = containerObj.GetComponent<Image>();
        containerImg.color = new Color(0.12f, 0.12f, 0.14f, 0.95f); // Sleek dark slate
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector4(450, 320);

        // Vertical Layout Group
        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 15;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;

        // Dungeon Title
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI title = titleObj.GetComponent<TextMeshProUGUI>();
        title.text = selectedDungeonName;
        title.fontSize = 26;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.97f, 0.76f, 0.17f); // Harmonic yellow

        // Description
        GameObject descObj = new GameObject("DescText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI desc = descObj.GetComponent<TextMeshProUGUI>();
        desc.text = "Tiến vào hầm mỏ bỏ hoang và tiêu diệt Boss Ogre để giành lấy rương báu cổ xưa.";
        desc.fontSize = 15;
        desc.alignment = TextAlignmentOptions.Center;
        desc.color = new Color(0.85f, 0.85f, 0.85f);
        
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(410, 60);

        // Energy Indicator
        GameObject energyObj = new GameObject("EnergyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        energyObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI energy = energyObj.GetComponent<TextMeshProUGUI>();
        energy.text = $"Năng Lượng Yêu Cầu: <color=#FF5722>{energyCost}</color> | Của Bạn: <color=#4CAF50>{playerEnergy}</color>";
        energy.fontSize = 16;
        energy.alignment = TextAlignmentOptions.Center;
        energy.color = Color.white;

        // Button container
        GameObject btnContainerObj = new GameObject("Buttons", typeof(RectTransform));
        btnContainerObj.transform.SetParent(containerObj.transform, false);
        RectTransform btnContainerRect = btnContainerObj.GetComponent<RectTransform>();
        btnContainerRect.sizeDelta = new Vector2(410, 50);

        // Start Button
        GameObject startBtnObj = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        startBtnObj.transform.SetParent(btnContainerObj.transform, false);
        RectTransform startBtnRect = startBtnObj.GetComponent<RectTransform>();
        startBtnRect.anchorMin = new Vector2(0.5f, 0);
        startBtnRect.anchorMax = new Vector2(0.5f, 0);
        startBtnRect.anchoredPosition = new Vector2(60, 0);
        startBtnRect.sizeDelta = new Vector2(110, 40);
        Image startBtnImg = startBtnObj.GetComponent<Image>();
        startBtnImg.color = new Color(0.2f, 0.6f, 0.2f); // Sleek Green
        Button startBtn = startBtnObj.GetComponent<Button>();
        startBtn.onClick.AddListener(OnStartClick);

        GameObject startBtnTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        startBtnTxtObj.transform.SetParent(startBtnObj.transform, false);
        TextMeshProUGUI startBtnTxt = startBtnTxtObj.GetComponent<TextMeshProUGUI>();
        startBtnTxt.text = "Thám Hiểm";
        startBtnTxt.fontSize = 14;
        startBtnTxt.fontStyle = FontStyles.Bold;
        startBtnTxt.alignment = TextAlignmentOptions.Center;
        startBtnTxt.color = Color.white;
        RectTransform startTxtRect = startBtnTxtObj.GetComponent<RectTransform>();
        startTxtRect.anchorMin = Vector2.zero;
        startTxtRect.anchorMax = Vector2.one;
        startTxtRect.sizeDelta = Vector2.zero;

        // Cancel Button
        GameObject cancelBtnObj = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelBtnObj.transform.SetParent(btnContainerObj.transform, false);
        RectTransform cancelBtnRect = cancelBtnObj.GetComponent<RectTransform>();
        cancelBtnRect.anchorMin = new Vector2(0.5f, 0);
        cancelBtnRect.anchorMax = new Vector2(0.5f, 0);
        cancelBtnRect.anchoredPosition = new Vector2(-60, 0);
        cancelBtnRect.sizeDelta = new Vector2(110, 40);
        Image cancelBtnImg = cancelBtnObj.GetComponent<Image>();
        cancelBtnImg.color = new Color(0.6f, 0.2f, 0.2f); // Sleek Red
        Button cancelBtn = cancelBtnObj.GetComponent<Button>();
        cancelBtn.onClick.AddListener(Close);

        GameObject cancelBtnTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        cancelBtnTxtObj.transform.SetParent(cancelBtnObj.transform, false);
        TextMeshProUGUI cancelBtnTxt = cancelBtnTxtObj.GetComponent<TextMeshProUGUI>();
        cancelBtnTxt.text = "Đóng";
        cancelBtnTxt.fontSize = 14;
        cancelBtnTxt.fontStyle = FontStyles.Bold;
        cancelBtnTxt.alignment = TextAlignmentOptions.Center;
        cancelBtnTxt.color = Color.white;
        RectTransform cancelTxtRect = cancelBtnTxtObj.GetComponent<RectTransform>();
        cancelTxtRect.anchorMin = Vector2.zero;
        cancelTxtRect.anchorMax = Vector2.one;
        cancelTxtRect.sizeDelta = Vector2.zero;
    }
}
