using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using System.Collections.Generic;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public class GachaUIManager : MonoBehaviour
{
    [Header("--- Banner Info ---")]
    public int currentBannerId = 1;
    public TextMeshProUGUI bannerNameText;
    public TextMeshProUGUI pityText;
    public Image bannerImage;
    public TextMeshProUGUI ticketCountText;

    [Header("--- Action Buttons ---")]
    public Button btnCloseMain;
    public Button btnPull1;
    public TextMeshProUGUI pull1CostText;
    public Button btnPull10;
    public TextMeshProUGUI pull10CostText;

    [Header("--- Gacha Result Popup ---")]
    public GameObject resultPopupPanel;
    public Transform resultItemContainer;
    public GameObject pulledItemPrefab;
    public Button btnCloseResult;

    [Header("--- Gacha Detail & Rates ---")]
    public Button btnOpenDetail;
    public Button btnCloseDetail;
    public GameObject detailPanel;
    public Transform detailItemContainer;
    public GameObject detailItemPrefab;
    public GameObject noDetailText;

    [Header("--- Detail Pagination ---")]
    public Button btnDetailPrevPage;
    public Button btnDetailNextPage;
    public TextMeshProUGUI detailPageNumberText;
    [Tooltip("Number of rate rows displayed per page")]
    public int detailPageSize = 5;

    [Header("--- Gacha History ---")]
    public Button btnOpenHistory;
    public Button btnCloseHistory;
    public GameObject historyPanel;
    public Transform historyItemContainer;
    public GameObject historyItemPrefab;
    public GameObject noHistoryText;

    [Header("--- History Pagination ---")]
    public Button btnPrevPage;
    public Button btnNextPage;
    public TextMeshProUGUI pageNumberText;
    [Tooltip("Number of history rows displayed per page")]
    public int historyPageSize = 5;

    [Header("--- Rarity Icons ---")]
    public Sprite iconCommon;
    public Sprite iconUncommon;
    public Sprite iconRare;
    public Sprite iconEpic;
    public Sprite iconLegendary;
    public Sprite iconMythic;

    [Header("--- Rarity Backgrounds (Result Cards) ---")]
    public Sprite bgCommon;
    public Sprite bgUncommon;
    public Sprite bgRare;
    public Sprite bgEpic;
    public Sprite bgLegendary;
    public Sprite bgMythic;

    private int _historyPage = 1;
    private int _historyTotalPages = 1;
    private bool _isLoadingHistory;

    private int _detailPage = 1;
    private int _detailTotalPages = 1;

    // 👇 KHU VỰC MỚI BỔ SUNG: WARNING POPUP
    [Header("--- Warning Popup ---")]
    public GameObject warningPopupPanel;
    public TextMeshProUGUI warningMessageText;
    public Button btnCloseWarning;

    [Header("--- Gacha Video Animation ---")]
    public VideoPlayer videoPlayer;
    public RawImage videoRawImage;
    public GameObject videoPanel;
    public Button btnSkipVideo;
    public VideoClip videoClipX1;
    public VideoClip videoClipX10;

    private System.Action _onVideoComplete;
    private bool _isVideoPlaying;
    private RenderTexture _videoTexture;

    [Header("--- Free Pull ---")]
    public TextMeshProUGUI freeCountdownText;
    private const string LastFreePullKey = "LastFreePullTime";
    private int _pull1CostCache = 0;

    private int _pityLimit = 90;
    private List<GachaBannerItemResponse> _cachedBannerItems = new List<GachaBannerItemResponse>();

    private void Awake()
    {
        SetupHoverEffects();
    }

    /// <summary>
    /// Gắn hiệu ứng phóng to khi rê chuột cho toàn bộ nút của GachaPanel,
    /// dùng đúng component UIHoverScaleEffect mà HUD đang dùng.
    /// </summary>
    private void SetupHoverEffects()
    {
        AddHoverEffect(btnCloseMain);
        AddHoverEffect(btnPull1);
        AddHoverEffect(btnPull10);
        AddHoverEffect(btnCloseResult);

        AddHoverEffect(btnOpenDetail);
        AddHoverEffect(btnCloseDetail);
        AddHoverEffect(btnDetailPrevPage);
        AddHoverEffect(btnDetailNextPage);

        AddHoverEffect(btnOpenHistory);
        AddHoverEffect(btnCloseHistory);
        AddHoverEffect(btnPrevPage);
        AddHoverEffect(btnNextPage);

        AddHoverEffect(btnCloseWarning);
    }

    private static void AddHoverEffect(Button btn)
    {
        if (btn == null) return;
        if (btn.GetComponent<UIHoverScaleEffect>() == null)
            btn.gameObject.AddComponent<UIHoverScaleEffect>();
    }

    private void OnEnable()
    {
        if (btnCloseMain == null)
        {
            Transform mainCloseTr = transform.Find("Header/CloseButton");
            if (mainCloseTr != null) btnCloseMain = mainCloseTr.GetComponent<Button>();
        }

        if (btnCloseMain != null)
        {
            btnCloseMain.onClick.RemoveAllListeners();
            btnCloseMain.onClick.AddListener(CloseMainPanel);
            AddHoverEffect(btnCloseMain);
        }

        if (btnPull1 != null)
        {
            btnPull1.onClick.RemoveAllListeners();
            btnPull1.onClick.AddListener(() => PerformPull(1));
        }
        
        if (btnPull10 != null)
        {
            btnPull10.onClick.RemoveAllListeners();
            btnPull10.onClick.AddListener(() => PerformPull(10));
        }
        
        if (btnCloseResult != null) 
        {
            btnCloseResult.onClick.RemoveAllListeners();
            btnCloseResult.onClick.AddListener(CloseResultPopup);
            btnCloseResult.transform.SetAsLastSibling();
        }

        if (btnOpenDetail != null) 
        {
            btnOpenDetail.onClick.RemoveAllListeners();
            btnOpenDetail.onClick.AddListener(OpenDetailPanel);
        }
        
        if (btnCloseDetail != null) 
        {
            btnCloseDetail.onClick.RemoveAllListeners();
            btnCloseDetail.onClick.AddListener(() => detailPanel.SetActive(false));
            btnCloseDetail.transform.SetAsLastSibling();
        }

        if (btnOpenHistory != null) 
        {
            btnOpenHistory.onClick.RemoveAllListeners();
            btnOpenHistory.onClick.AddListener(OpenHistoryPanel);
        }
        
        if (btnCloseHistory != null) 
        {
            btnCloseHistory.onClick.RemoveAllListeners();
            btnCloseHistory.onClick.AddListener(() => 
            {
                Debug.Log("[GachaUIManager] History CloseButton clicked!");
                if (historyPanel != null) historyPanel.SetActive(false);
            });
            // Giúp nút close không bị các element khác đè lên (nếu có)
            btnCloseHistory.transform.SetAsLastSibling();
        }

        if (btnPrevPage != null) 
        {
            btnPrevPage.onClick.RemoveAllListeners();
            btnPrevPage.onClick.AddListener(() => ChangeHistoryPage(-1));
        }
        
        if (btnNextPage != null) 
        {
            btnNextPage.onClick.RemoveAllListeners();
            btnNextPage.onClick.AddListener(() => ChangeHistoryPage(1));
        }

        if (btnDetailPrevPage != null) 
        {
            btnDetailPrevPage.onClick.RemoveAllListeners();
            btnDetailPrevPage.onClick.AddListener(() => ChangeDetailPage(-1));
        }
        
        if (btnDetailNextPage != null) 
        {
            btnDetailNextPage.onClick.RemoveAllListeners();
            btnDetailNextPage.onClick.AddListener(() => ChangeDetailPage(1));
        }

        // Bật lắng nghe nút đóng cảnh báo
        if (btnCloseWarning != null) 
        {
            btnCloseWarning.onClick.RemoveAllListeners();
            btnCloseWarning.onClick.AddListener(CloseWarningPopup);
            btnCloseWarning.transform.SetAsLastSibling();
        }

        // Ẩn tất cả các bảng phụ
        if (resultPopupPanel != null) resultPopupPanel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
        if (historyPanel != null) historyPanel.SetActive(false);
        if (warningPopupPanel != null) warningPopupPanel.SetActive(false); // Ẩn cảnh báo

        LoadBannerData(currentBannerId);
        LoadUserTicketCount();
    }

    private void OnDisable()
    {
        if (btnCloseMain != null) btnCloseMain.onClick.RemoveAllListeners();
        if (btnPull1 != null) btnPull1.onClick.RemoveAllListeners();
        btnPull10.onClick.RemoveAllListeners();
        if (btnCloseResult != null) btnCloseResult.onClick.RemoveAllListeners();
        if (btnOpenDetail != null) btnOpenDetail.onClick.RemoveAllListeners();
        if (btnCloseDetail != null) btnCloseDetail.onClick.RemoveAllListeners();
        if (btnOpenHistory != null) btnOpenHistory.onClick.RemoveAllListeners();
        if (btnCloseHistory != null) btnCloseHistory.onClick.RemoveAllListeners();
        if (btnPrevPage != null) btnPrevPage.onClick.RemoveAllListeners();
        if (btnNextPage != null) btnNextPage.onClick.RemoveAllListeners();
        if (btnDetailPrevPage != null) btnDetailPrevPage.onClick.RemoveAllListeners();
        if (btnDetailNextPage != null) btnDetailNextPage.onClick.RemoveAllListeners();
        if (btnCloseWarning != null) btnCloseWarning.onClick.RemoveAllListeners();
        if (btnSkipVideo != null) btnSkipVideo.onClick.RemoveAllListeners();
    }

    private void OnDestroy()
    {
        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
            _videoTexture = null;
        }
    }

    private void CloseMainPanel()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClosePanel(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void LoadBannerData(int bannerId)
    {
        SetButtonsInteractable(false);

        GachaApi.Instance.GetById(bannerId,
            onSuccess: (response) =>
            {
                if (bannerNameText != null) bannerNameText.text = response.Name;
                if (pull1CostText != null) 
                {
                    _pull1CostCache = response.PullCost;
                    pull1CostText.text = response.PullCost.ToString();
                }
                if (pull10CostText != null) pull10CostText.text = (response.PullCost * 10).ToString();

                _pityLimit = response.PityLimit;
                _cachedBannerItems = response.BannerItems ?? new List<GachaBannerItemResponse>();

                LoadCurrentPityFromHistory();
                SetButtonsInteractable(true);
            },
            onError: (error) => { Debug.LogError("[GachaUI] Failed to load Banner: " + error.Message); }
        );
    }

    private void LoadUserTicketCount()
    {
        if (ticketCountText == null) return;

        ticketCountText.text = "x0";

        InventoryApi.Instance.GetInventory(
            onSuccess: (inventory) =>
            {
                if (ticketCountText == null) return;

                int ticketCount = 0;
                if (inventory?.BagItems != null)
                {
                    foreach (var item in inventory.BagItems)
                    {
                        if (IsGachaTicketItem(item))
                        {
                            ticketCount += item.Quantity;
                        }
                    }
                }

                ticketCountText.text = $"x{ticketCount}";
            },
            onError: (error) =>
            {
                Debug.LogWarning("[GachaUI] Failed to get ticket count: " + error.Message);
                if (ticketCountText != null) ticketCountText.text = "x0";
            }
        );
    }

    private bool IsGachaTicketItem(InventoryItemResponse item)
    {
        if (item == null || string.IsNullOrEmpty(item.ItemName)) return false;
        return item.ItemName.Contains("Lucky Ticket", System.StringComparison.OrdinalIgnoreCase);
    }

    private void OpenDetailPanel()
    {
        if (detailPanel == null || detailItemContainer == null || detailItemPrefab == null)
        {
            Debug.LogWarning("[GachaUI] DetailPanel missing references (detailItemContainer/detailItemPrefab).");
            return;
        }

        _detailPage = 1;
        detailPanel.SetActive(true);
        RenderDetailPage(_detailPage);
    }

    private void ChangeDetailPage(int delta)
    {
        int target = Mathf.Clamp(_detailPage + delta, 1, _detailTotalPages);
        if (target == _detailPage) return;
        _detailPage = target;
        RenderDetailPage(_detailPage);
    }

    /// <summary>
    /// Tỉ lệ rơi đã nằm sẵn trong _cachedBannerItems nên phân trang hoàn toàn ở client,
    /// không gọi lại API như bên lịch sử.
    /// </summary>
    private void RenderDetailPage(int page)
    {
        foreach (Transform child in detailItemContainer) Destroy(child.gameObject);

        int totalCount = _cachedBannerItems.Count;
        _detailTotalPages = Mathf.Max(1, (totalCount + detailPageSize - 1) / detailPageSize);
        _detailPage = Mathf.Clamp(page, 1, _detailTotalPages);

        // Bảng tỉ lệ xếp theo độ hiếm giảm dần, vật phẩm nổi bật lên đầu.
        var ordered = new List<GachaBannerItemResponse>(_cachedBannerItems);
        ordered.Sort((a, b) =>
        {
            if (a.IsFeatured != b.IsFeatured) return b.IsFeatured.CompareTo(a.IsFeatured);
            int rankCompare = GetRarityRank(b.ItemRarity).CompareTo(GetRarityRank(a.ItemRarity));
            if (rankCompare != 0) return rankCompare;
            return a.DropRate.CompareTo(b.DropRate);
        });

        int startIndex = (_detailPage - 1) * detailPageSize;
        int endIndex = Mathf.Min(startIndex + detailPageSize, totalCount);

        for (int i = startIndex; i < endIndex; i++)
        {
            var item = ordered[i];
            GameObject go = Instantiate(detailItemPrefab, detailItemContainer);
            GachaDetailItemUI binder = go != null ? go.GetComponent<GachaDetailItemUI>() : null;
            if (binder == null)
            {
                Debug.LogWarning("[GachaUI] detailItemPrefab is missing GachaDetailItemUI component.");
                continue;
            }

            string rarityColor = GetRarityColorHex(item.ItemRarity);

            if (binder.typeText != null)
                binder.typeText.text = string.IsNullOrEmpty(item.ItemRarity)
                    ? ""
                    : $"<color={rarityColor}>{item.ItemRarity}</color>";

            if (binder.itemNameText != null)
                binder.itemNameText.text = $"<color={rarityColor}>{item.ItemName}</color>";

            if (binder.rateText != null)
                binder.rateText.text = $"{item.DropRate}%";

            if (binder.rarityIconImage != null)
            {
                Sprite gem = GetRarityIcon(item.ItemRarity);
                binder.rarityIconImage.sprite = gem;
                binder.rarityIconImage.enabled = gem != null;
            }

            if (binder.itemIconImage != null)
            {
                Sprite icon = null;
                if (ItemIconDatabase.Instance != null)
                    icon = ItemIconDatabase.Instance.GetIcon(item.ItemName, item.ItemRarity);

                if (icon == null)
                    icon = Resources.Load<Sprite>($"Icons/{item.ItemName}") ?? Resources.Load<Sprite>($"Icons/{item.ItemRarity}");

                binder.itemIconImage.sprite = icon;
                binder.itemIconImage.enabled = icon != null;
            }
        }

        if (noDetailText != null) noDetailText.SetActive(totalCount == 0);

        SetDetailButtonsInteractable(true);
    }

    private void SetDetailButtonsInteractable(bool state)
    {
        if (btnDetailPrevPage != null) btnDetailPrevPage.interactable = state && _detailPage > 1;
        if (btnDetailNextPage != null) btnDetailNextPage.interactable = state && _detailPage < _detailTotalPages;
        if (detailPageNumberText != null) detailPageNumberText.text = $"{_detailPage}/{_detailTotalPages}";
    }

    private int GetRarityRank(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return 0;
        switch (rarity.ToLower())
        {
            case "mythic": return 6;
            case "legendary": return 5;
            case "epic": return 4;
            case "rare": return 3;
            case "uncommon": return 2;
            case "common": return 1;
            default: return 0;
        }
    }

    private void OpenHistoryPanel()
    {
        if (historyPanel == null || historyItemContainer == null || historyItemPrefab == null)
        {
            Debug.LogWarning("[GachaUI] HistoryPanel missing references (historyItemContainer/historyItemPrefab).");
            return;
        }

        _historyPage = 1;
        historyPanel.SetActive(true);
        LoadHistoryPage(_historyPage);
    }

    private void ChangeHistoryPage(int delta)
    {
        if (_isLoadingHistory) return;
        int target = Mathf.Clamp(_historyPage + delta, 1, _historyTotalPages);
        if (target == _historyPage) return;
        _historyPage = target;
        LoadHistoryPage(_historyPage);
    }

    private void LoadHistoryPage(int page)
    {
        _isLoadingHistory = true;
        SetHistoryButtonsInteractable(false);

        GachaApi.Instance.GetHistory(page, historyPageSize,
            onSuccess: (response) =>
            {
                _isLoadingHistory = false;

                if (response == null || response.Items == null)
                {
                    Debug.LogWarning("[GachaUI] GetHistory returned empty data.");
                    SetHistoryButtonsInteractable(false);
                    return;
                }

                foreach (Transform child in historyItemContainer) Destroy(child.gameObject);

                int totalCount = response.TotalCount;
                int totalPages = Mathf.Max(1, (totalCount + historyPageSize - 1) / historyPageSize);
                if (_historyTotalPages != totalPages) _historyTotalPages = totalPages;
                _historyPage = Mathf.Clamp(page, 1, _historyTotalPages);

                int shown = 0;
                foreach (var history in response.Items)
                {
                    GameObject go = Instantiate(historyItemPrefab, historyItemContainer);
                    GachaHistoryItemUI binder = go != null ? go.GetComponent<GachaHistoryItemUI>() : null;
                    if (binder != null)
                    {
                        string rarityColor = GetRarityColorHex(history.RewardItemRarity);
                        if (binder.typeText != null) binder.typeText.text = string.IsNullOrEmpty(history.RewardItemRarity) ? "" : history.RewardItemRarity;
                        if (binder.itemNameText != null) binder.itemNameText.text = $"<color={rarityColor}>{history.RewardItemName}</color>";
                        if (binder.dateTimeText != null) binder.dateTimeText.text = history.PulledAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                        if (binder.rarityIconImage != null)
                        {
                            Sprite icon = GetRarityIcon(history.RewardItemRarity);
                            binder.rarityIconImage.sprite = icon;
                            binder.rarityIconImage.enabled = icon != null;
                        }
                    }
                    else
                    {
                        TextMeshProUGUI txt = go.GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null)
                        {
                            string rarityColor = GetRarityColorHex(history.RewardItemRarity);
                            txt.text = $"[{history.PulledAt.ToLocalTime():dd/MM HH:mm}] Pulled: <color={rarityColor}>{history.RewardItemName}</color>";
                        }
                    }
                    shown++;
                }

                if (noHistoryText != null)
                {
                    noHistoryText.SetActive(shown == 0);
                }

                SetHistoryButtonsInteractable(true);
            },
            onError: (error) =>
            {
                _isLoadingHistory = false;
                Debug.LogError("[GachaUI] Failed to load history: " + error.Message);
                SetHistoryButtonsInteractable(false);
            }
        );
    }

    private void SetHistoryButtonsInteractable(bool state)
    {
        if (btnPrevPage != null) btnPrevPage.interactable = state && _historyPage > 1;
        if (btnNextPage != null) btnNextPage.interactable = state && _historyPage < _historyTotalPages;
        if (pageNumberText != null) pageNumberText.text = $"{_historyPage}/{_historyTotalPages}";
    }

    private Sprite GetRarityIcon(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return iconCommon;
        switch (rarity.ToLower())
        {
            case "common": return iconCommon;
            case "uncommon": return iconUncommon;
            case "rare": return iconRare;
            case "epic": return iconEpic;
            case "legendary": return iconLegendary;
            case "mythic": return iconMythic;
            default: return iconCommon;
        }
    }

    /// <summary>
    /// Khung nền của thẻ kết quả đổi theo độ hiếm.
    /// Sprite lấy từ các ô kéo trong Inspector nên đổi ảnh không cần sửa code.
    /// </summary>
    private Sprite GetRarityBackground(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return bgCommon;
        switch (rarity.ToLower())
        {
            case "common": return bgCommon;
            case "uncommon": return bgUncommon;
            case "rare": return bgRare;
            case "epic": return bgEpic;
            case "legendary": return bgLegendary;
            case "mythic": return bgMythic;
            default: return bgCommon;
        }
    }

    private void LoadCurrentPityFromHistory()
    {
        GachaApi.Instance.GetHistory(1, 100,
            onSuccess: (response) =>
            {
                if (response?.Items == null || response.Items.Length == 0)
                {
                    UpdatePityUI(0);
                    return;
                }

                int currentPity = 0;
                var featuredItemIds = new HashSet<int>();
                foreach (var item in _cachedBannerItems)
                {
                    if (item.IsFeatured) featuredItemIds.Add(item.ItemId);
                }

                foreach (var history in response.Items)
                {
                    if (featuredItemIds.Contains(history.RewardItemId))
                    {
                        currentPity = 0;
                        break;
                    }

                    currentPity++;
                }

                UpdatePityUI(currentPity);
            },
            onError: (error) =>
            {
                Debug.LogWarning("[GachaUI] Failed to get pity from history: " + error.Message);
                UpdatePityUI(0);
            }
        );
    }

    private void Update()
    {
        UpdateFreePullUI();
    }

    private void UpdateFreePullUI()
    {
        if (pull1CostText == null || _pull1CostCache == 0) return;

        if (IsFreePullAvailable())
        {
            pull1CostText.text = "Free";
            if (freeCountdownText != null)
            {
                freeCountdownText.text = "";
                freeCountdownText.gameObject.SetActive(false);
            }
        }
        else
        {
            pull1CostText.text = _pull1CostCache.ToString();
            if (freeCountdownText != null)
            {
                freeCountdownText.gameObject.SetActive(true);
                System.TimeSpan timeleft = GetNextFreePullTime() - System.DateTime.Now;
                if (timeleft.TotalSeconds < 0) timeleft = System.TimeSpan.Zero;
                freeCountdownText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", timeleft.Hours, timeleft.Minutes, timeleft.Seconds);
            }
        }
    }

    private bool IsFreePullAvailable()
    {
        return System.DateTime.Now >= GetNextFreePullTime();
    }

    private System.DateTime GetNextFreePullTime()
    {
        string timeStr = PlayerPrefs.GetString(LastFreePullKey, "");
        if (string.IsNullOrEmpty(timeStr)) return System.DateTime.MinValue;
        if (System.DateTime.TryParse(timeStr, out System.DateTime lastTime))
        {
            return lastTime.AddHours(24);
        }
        return System.DateTime.MinValue;
    }

    private void UseFreePull()
    {
        PlayerPrefs.SetString(LastFreePullKey, System.DateTime.Now.ToString("O"));
        PlayerPrefs.Save();
    }

    private void PerformPull(int amount)
    {
        bool isFreePull = false;
        if (amount == 1 && IsFreePullAvailable())
        {
            isFreePull = true;
            UseFreePull();
        }

        SetButtonsInteractable(false);
        GachaApi.Instance.Pull(currentBannerId, amount, isFreePull,
            onSuccess: (result) =>
            {
                VideoClip clipToPlay = ResolveVideoClip(amount);
                PlayGachaVideo(clipToPlay, () =>
                {
                    ShowResultPopup(result);
                    LoadUserTicketCount();
                    InventoryManager.RefreshAny(refreshStats: true);
                });
            },
            onError: (error) =>
            {
                Debug.LogWarning("[GachaUI] Pull failed: " + error.Message);

                // Nếu có lỗi, hoàn lại lượt free
                if (isFreePull)
                {
                    PlayerPrefs.DeleteKey(LastFreePullKey);
                    PlayerPrefs.Save();
                }

                ShowWarningPopup("Not enough tickets or an error occurred!\n" + error.Message);
                SetButtonsInteractable(true);
            }
        );
    }

    private VideoClip ResolveVideoClip(int amount)
    {
        VideoClip clip = (amount >= 10) ? videoClipX10 : videoClipX1;

#if UNITY_EDITOR
        if (clip == null)
        {
            string path = (amount >= 10) ? "Assets/UI/Videos/GachaX10.mp4" : "Assets/UI/Videos/GachaX1.mp4";
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(path);
        }
#endif

        if (clip == null)
        {
            string resName = (amount >= 10) ? "Videos/GachaX10" : "Videos/GachaX1";
            clip = Resources.Load<VideoClip>(resName);
        }

        return clip;
    }

    private void PlayGachaVideo(VideoClip clip, System.Action onComplete)
    {
        if (clip == null)
        {
            Debug.LogWarning("[GachaUI] VideoClip is null, skipping video animation.");
            onComplete?.Invoke();
            return;
        }

        EnsureVideoComponents();

        // Reparent videoPanel lên Canvas gốc của màn hình để đảm bảo tràn viền FULL SCREEN 100%
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.name.Equals("Canvas", System.StringComparison.OrdinalIgnoreCase))
                {
                    rootCanvas = c;
                    break;
                }
            }
        }

        if (rootCanvas != null && videoPanel != null)
        {
            if (videoPanel.transform.parent != rootCanvas.transform)
            {
                videoPanel.transform.SetParent(rootCanvas.transform, false);
            }
        }

        if (videoPanel != null)
        {
            var canvasComp = videoPanel.GetComponent<Canvas>();
            if (canvasComp == null) canvasComp = videoPanel.AddComponent<Canvas>();
            canvasComp.overrideSorting = true;
            canvasComp.sortingOrder = 9998;

            if (videoPanel.GetComponent<GraphicRaycaster>() == null)
                videoPanel.AddComponent<GraphicRaycaster>();

            RectTransform rt = videoPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            videoPanel.transform.SetAsLastSibling();
            videoPanel.SetActive(true);
        }

        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
        }

        // Tự động điều chỉnh độ phân giải RenderTexture theo video gốc hoặc màn hình máy người chơi
        int texW = (clip != null && clip.width > 0) ? (int)clip.width : Screen.width;
        int texH = (clip != null && clip.height > 0) ? (int)clip.height : Screen.height;
        if (texW <= 0) texW = 1920;
        if (texH <= 0) texH = 1080;

        _videoTexture = new RenderTexture(texW, texH, 16, RenderTextureFormat.ARGB32);
        _videoTexture.Create();

        if (videoRawImage != null)
        {
            RectTransform rawRt = videoRawImage.GetComponent<RectTransform>();
            rawRt.anchorMin = new Vector2(0.5f, 0.5f);
            rawRt.anchorMax = new Vector2(0.5f, 0.5f);
            rawRt.pivot = new Vector2(0.5f, 0.5f);
            rawRt.anchoredPosition = Vector2.zero;
            rawRt.sizeDelta = new Vector2(Screen.width, Screen.height);
            rawRt.localScale = Vector3.one;

            // AspectRatioFitter EnvelopeParent giúp video tự động co giãn vừa khít 100% mọi tỉ lệ màn hình
            // (16:9, 16:10, 21:9 Ultrawide...) mà không bị méo hình hay hở viền đen
            var fitter = videoRawImage.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = videoRawImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)texW / (float)texH;

            videoRawImage.texture = _videoTexture;
            videoRawImage.color = Color.white;
        }

        if (videoPlayer != null)
        {
            videoPlayer.targetTexture = _videoTexture;
            videoPlayer.clip = clip;
            videoPlayer.isLooping = false;
            videoPlayer.playOnAwake = false;
        }

        if (videoRawImage != null)
        {
            videoRawImage.texture = _videoTexture;
        }

        _onVideoComplete = onComplete;
        _isVideoPlaying = true;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
            videoPlayer.Play();
        }
        else
        {
            FinishGachaVideo();
        }
    }

    private void OnVideoLoopPointReached(VideoPlayer vp)
    {
        FinishGachaVideo();
    }

    private void SkipGachaVideo()
    {
        FinishGachaVideo();
    }

    private void FinishGachaVideo()
    {
        if (!_isVideoPlaying && (videoPanel == null || !videoPanel.activeSelf)) return;
        _isVideoPlaying = false;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.Stop();
        }

        if (videoPanel != null) videoPanel.SetActive(false);

        var callback = _onVideoComplete;
        _onVideoComplete = null;
        callback?.Invoke();
    }

    private void EnsureVideoComponents()
    {
        if (videoPanel == null)
        {
            Transform found = transform.Find("VideoPanel") ?? transform.Find("GachaVideoPanel");
            if (found != null)
            {
                videoPanel = found.gameObject;
            }
            else
            {
                videoPanel = new GameObject("GachaVideoPanel", typeof(RectTransform));
                videoPanel.transform.SetParent(transform, false);
            }
        }

        if (videoPanel != null)
        {
            // Bắt buộc Canvas con overrideSorting = 9998 phủ KÍN TOÀN MÀN HÌNH (Full Screen)
            var canvas = videoPanel.GetComponent<Canvas>();
            if (canvas == null) canvas = videoPanel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9998;

            if (videoPanel.GetComponent<GraphicRaycaster>() == null)
                videoPanel.AddComponent<GraphicRaycaster>();

            RectTransform rt = videoPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            videoPanel.transform.SetAsLastSibling();
        }

        if (videoRawImage == null && videoPanel != null)
        {
            videoRawImage = videoPanel.GetComponentInChildren<RawImage>(true);
            if (videoRawImage == null)
            {
                GameObject rawGo = new GameObject("VideoRawImage", typeof(RectTransform), typeof(RawImage));
                rawGo.transform.SetParent(videoPanel.transform, false);
                videoRawImage = rawGo.GetComponent<RawImage>();
            }
        }

        if (videoRawImage != null)
        {
            RectTransform rt = videoRawImage.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            videoRawImage.color = Color.white;
            videoRawImage.raycastTarget = false;
        }

        if (videoPlayer == null && videoPanel != null)
        {
            videoPlayer = videoPanel.GetComponent<VideoPlayer>();
            if (videoPlayer == null)
            {
                videoPlayer = videoPanel.AddComponent<VideoPlayer>();
            }
        }

        if (btnSkipVideo == null && videoPanel != null)
        {
            btnSkipVideo = videoPanel.GetComponentInChildren<Button>(true);
            if (btnSkipVideo == null)
            {
                GameObject btnGo = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(videoPanel.transform, false);
                RectTransform rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-40, -40);
                rt.sizeDelta = new Vector2(120, 44);

                Image img = btnGo.GetComponent<Image>();
                img.color = new Color(0, 0, 0, 0.75f);

                btnSkipVideo = btnGo.GetComponent<Button>();

                GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(btnGo.transform, false);
                RectTransform textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI txt = textGo.GetComponent<TextMeshProUGUI>();
                txt.text = "SKIP >>";
                txt.alignment = TextAlignmentOptions.Center;
                txt.fontSize = 18;
                txt.fontStyle = FontStyles.Bold;
                txt.color = Color.white;
            }
        }

        if (btnSkipVideo != null)
        {
            btnSkipVideo.onClick.RemoveAllListeners();
            btnSkipVideo.onClick.AddListener(SkipGachaVideo);
            btnSkipVideo.transform.SetAsLastSibling();
        }
    }

    // 👇 CÁC HÀM XỬ LÝ WARNING POPUP
    private void ShowWarningPopup(string message)
    {
        if (warningPopupPanel == null) return;
        if (warningMessageText != null) warningMessageText.text = message;
        warningPopupPanel.SetActive(true);
    }

    private void CloseWarningPopup()
    {
        if (warningPopupPanel != null) warningPopupPanel.SetActive(false);
    }

    private void ShowResultPopup(MultiPullResultResponse result)
    {
        foreach (Transform child in resultItemContainer) Destroy(child.gameObject);

        int currentPity = 0;

        if (result.PulledItems != null)
        {
            // Gộp item trùng thành 1 card + badge xN, giữ đúng thứ tự quay được
            var order = new List<GachaPullResultResponse>();
            var counts = new Dictionary<int, int>();

            foreach (var item in result.PulledItems)
            {
                // PulledItemId <= 0 là lần quay backend trả lỗi -> để riêng, không gộp
                if (item.PulledItemId > 0 && counts.ContainsKey(item.PulledItemId))
                {
                    counts[item.PulledItemId]++;
                }
                else
                {
                    if (item.PulledItemId > 0) counts[item.PulledItemId] = 1;
                    order.Add(item);
                }

                // Ưu tiên dùng pity trả về từ backend
                if (item.CurrentPity >= 0)
                {
                    currentPity = item.CurrentPity;
                }
            }

            foreach (var item in order)
            {
                GameObject newItemUI = Instantiate(pulledItemPrefab, resultItemContainer);

                GachaResultItemUI ui = newItemUI.GetComponent<GachaResultItemUI>();
                if (ui == null)
                {
                    Debug.LogWarning("[GachaUI] pulledItemPrefab thiếu component GachaResultItemUI.");
                }
                else
                {
                    // Khung nền đổi theo độ hiếm của vật phẩm vừa quay được
                    if (ui.typeBgImage != null)
                    {
                        Sprite bg = GetRarityBackground(item.PulledItemRarity);
                        if (bg != null) ui.typeBgImage.sprite = bg;
                    }

                    if (ui.itemIconImage != null)
                    {
                        Sprite icon = null;
                        if (ItemIconDatabase.Instance != null)
                        {
                            icon = ItemIconDatabase.Instance.GetIcon(item.PulledItemName, item.PulledItemRarity);
                        }

                        if (icon != null)
                        {
                            ui.itemIconImage.sprite = icon;
                            ui.itemIconImage.enabled = true;
                        }
                    }

                    string hexColor = GetRarityColorHex(item.PulledItemRarity);

                    ui.ApplyRarityVisuals(item.PulledItemRarity, hexColor);

                    // Prefab hiện tại không còn TMP tên vật phẩm, để trống thì bỏ qua
                    if (ui.itemNameText != null)
                    {
                        ui.itemNameText.text = $"<color={hexColor}>{item.PulledItemName}</color>";
                    }

                    int count;
                    ui.SetQuantity(counts.TryGetValue(item.PulledItemId, out count) ? count : 1, hexColor);
                }
            }
        }

        // Cập nhật lên UI bằng giá trị thật từ backend
        UpdatePityUI(currentPity);
        resultPopupPanel.SetActive(true);
    }

    private void CloseResultPopup()
    {
        resultPopupPanel.SetActive(false);
        SetButtonsInteractable(true);
    }

    private void UpdatePityUI(int currentPity)
    {
        if (pityText == null) return;

        int pullsLeft = _pityLimit - currentPity;

        // Càng gần số 0 thì màu càng đỏ rực lên
        string colorTag = pullsLeft <= 10 ? "<color=red>" : "<color=yellow>";

        pityText.text = $"Guaranteed rare item in: {colorTag}{pullsLeft}</color> pulls";
    }

    private void SetButtonsInteractable(bool state)
    {
        if (btnPull1 != null) btnPull1.interactable = state;
        if (btnPull10 != null) btnPull10.interactable = state;
    }

    private string GetRarityColorHex(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return "#C0C7D1";
        switch (rarity.Trim().ToLower())
        {
            case "mythic": return "#FF3340";    // Crimson Red
            case "legendary": return "#FFC726"; // Gold
            case "epic": return "#B847FF";      // Purple
            case "rare": return "#26A6FF";      // Cyan Blue
            case "uncommon": return "#40E066";  // Green
            case "common":
            default: return "#C0C7D1";          // Silver
        }
    }
}