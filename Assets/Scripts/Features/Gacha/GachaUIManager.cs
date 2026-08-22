using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using System.Collections.Generic;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

// Executes core business logic for mono behaviour.
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
    private int _pull1CostCache = 0;
    private bool _freePullStateLoaded;
    private System.DateTime _lastFreePullUtc = System.DateTime.MinValue;
    private System.DateTime _previousFreePullUtc = System.DateTime.MinValue;

    private int _pityLimit = 90;
    private List<GachaBannerItemResponse> _cachedBannerItems = new List<GachaBannerItemResponse>();

    // Initializes internal component caches and dependencies for GachaUIManager upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        DisableDecorativeRaycasts();
        SetupHoverEffects();
    }

    // Executes core business logic for disable decorative raycasts.
    private void DisableDecorativeRaycasts()
    {
        Transform decorationRoot = transform.Find("Deco");
        if (decorationRoot == null) return;

        foreach (Graphic graphic in decorationRoot.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }


    // Update up hover effects; it creates hover effect.
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
    }

    // Executes core business logic for add hover effect.
    private static void AddHoverEffect(Button btn)
    {
        if (btn == null) return;
        if (btn.GetComponent<UIHoverScaleEffect>() == null)
            btn.gameObject.AddComponent<UIHoverScaleEffect>();
    }

    // Wires up all button click listeners and triggers banner data fetching.
    private void OnEnable()
    {
        if (btnCloseMain == null)
        {
            Transform mainCloseTr = transform.Find("Header/CloseButton");
            if (mainCloseTr != null) btnCloseMain = mainCloseTr.GetComponent<Button>(); // Auto-locate main close button
        }

        if (btnCloseMain != null)
        {
            btnCloseMain.onClick.RemoveAllListeners();
            btnCloseMain.onClick.AddListener(CloseMainPanel); // Wire close modal trigger
            AddHoverEffect(btnCloseMain);
        }

        if (btnPull1 != null)
        {
            btnPull1.onClick.RemoveAllListeners();
            btnPull1.onClick.AddListener(() => PerformPull(1)); // Wire 1x pull trigger
        }

        if (btnPull10 != null)
        {
            btnPull10.onClick.RemoveAllListeners();
            btnPull10.onClick.AddListener(() => PerformPull(10)); // Wire 10x pull trigger
        }

        if (btnCloseResult != null)
        {
            btnCloseResult.onClick.RemoveAllListeners();
            btnCloseResult.onClick.AddListener(CloseResultPopup); // Wire result popup dismissal
            btnCloseResult.transform.SetAsLastSibling();
        }

        if (btnOpenDetail != null)
        {
            btnOpenDetail.onClick.RemoveAllListeners();
            btnOpenDetail.onClick.AddListener(OpenDetailPanel); // Wire drop rates detail modal
        }

        if (btnCloseDetail != null)
        {
            btnCloseDetail.onClick.RemoveAllListeners();
            btnCloseDetail.onClick.AddListener(() => detailPanel.SetActive(false)); // Close drop rate modal
            btnCloseDetail.transform.SetAsLastSibling();
        }

        if (btnOpenHistory != null)
        {
            btnOpenHistory.onClick.RemoveAllListeners();
            btnOpenHistory.onClick.AddListener(OpenHistoryPanel); // Wire pull history modal
        }

        if (btnCloseHistory != null)
        {
            btnCloseHistory.onClick.RemoveAllListeners();
            btnCloseHistory.onClick.AddListener(() =>
            {
                Debug.Log("[GachaUIManager] History CloseButton clicked!");
                if (historyPanel != null) historyPanel.SetActive(false); // Close history modal
            });
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

        if (resultPopupPanel != null) resultPopupPanel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
        if (historyPanel != null) historyPanel.SetActive(false);

        LoadBannerData(currentBannerId);
        LoadUserTicketCount();
        LoadFreePullState();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        if (btnCloseMain != null) btnCloseMain.onClick.RemoveAllListeners();
        if (btnPull1 != null) btnPull1.onClick.RemoveAllListeners();
        if (btnPull10 != null) btnPull10.onClick.RemoveAllListeners();
        if (btnCloseResult != null) btnCloseResult.onClick.RemoveAllListeners();
        if (btnOpenDetail != null) btnOpenDetail.onClick.RemoveAllListeners();
        if (btnCloseDetail != null) btnCloseDetail.onClick.RemoveAllListeners();
        if (btnOpenHistory != null) btnOpenHistory.onClick.RemoveAllListeners();
        if (btnCloseHistory != null) btnCloseHistory.onClick.RemoveAllListeners();
        if (btnPrevPage != null) btnPrevPage.onClick.RemoveAllListeners();
        if (btnNextPage != null) btnNextPage.onClick.RemoveAllListeners();
        if (btnDetailPrevPage != null) btnDetailPrevPage.onClick.RemoveAllListeners();
        if (btnDetailNextPage != null) btnDetailNextPage.onClick.RemoveAllListeners();
        if (btnSkipVideo != null) btnSkipVideo.onClick.RemoveAllListeners();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
            _videoTexture = null;
        }
    }

    // Executes core business logic for close main panel.
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

    // Executes core business logic for load banner data.
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

    // Executes core business logic for load user ticket count.
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

    // Executes core business logic for is gacha ticket item.
    // Logic details: validates required non-empty string arguments.
    // Returns a boolean indicating operation success.
    private bool IsGachaTicketItem(InventoryItemResponse item)
    {
        if (item == null || string.IsNullOrEmpty(item.ItemName)) return false;
        return item.ItemName.Contains("Lucky Ticket", System.StringComparison.OrdinalIgnoreCase);
    }

    // Executes core business logic for open detail panel.
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

    // Executes core business logic for change detail page.
    // Logic details: validates required non-empty string arguments.
    private void ChangeDetailPage(int delta)
    {
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        int target = Mathf.Clamp(_detailPage + delta, 1, _detailTotalPages);
        if (target == _detailPage) return;
        _detailPage = target;
        RenderDetailPage(_detailPage);
    }

    // Executes core business logic for render detail page.
    private void RenderDetailPage(int page)
    {
        foreach (Transform child in detailItemContainer) Destroy(child.gameObject);

        int totalCount = _cachedBannerItems.Count;
        _detailTotalPages = Mathf.Max(1, (totalCount + detailPageSize - 1) / detailPageSize);
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        _detailPage = Mathf.Clamp(page, 1, _detailTotalPages);

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

    // Executes core business logic for set detail buttons interactable.
    private void SetDetailButtonsInteractable(bool state)
    {
        if (btnDetailPrevPage != null) btnDetailPrevPage.interactable = state && _detailPage > 1;
        if (btnDetailNextPage != null) btnDetailNextPage.interactable = state && _detailPage < _detailTotalPages;
        if (detailPageNumberText != null) detailPageNumberText.text = $"{_detailPage}/{_detailTotalPages}";
    }

    // Executes core business logic for get rarity rank.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for open history panel.
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

    // Executes core business logic for change history page.
    // Logic details: validates required non-empty string arguments.
    private void ChangeHistoryPage(int delta)
    {
        if (_isLoadingHistory) return;
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        int target = Mathf.Clamp(_historyPage + delta, 1, _historyTotalPages);
        if (target == _historyPage) return;
        _historyPage = target;
        LoadHistoryPage(_historyPage);
    }

    // Executes core business logic for load history page.
    // Logic details: validates required non-empty string arguments.
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
                // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
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

    // Executes core business logic for set history buttons interactable.
    private void SetHistoryButtonsInteractable(bool state)
    {
        if (btnPrevPage != null) btnPrevPage.interactable = state && _historyPage > 1;
        if (btnNextPage != null) btnNextPage.interactable = state && _historyPage < _historyTotalPages;
        if (pageNumberText != null) pageNumberText.text = $"{_historyPage}/{_historyTotalPages}";
    }

    // Executes core business logic for get rarity icon.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for get rarity background.
    // Logic details: validates required non-empty string arguments.
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

    // Executes core business logic for load current pity from history.
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

    // Update the current state; it updates free pull ui.
    private void Update()
    {
        UpdateFreePullUI();
    }

    // Executes core business logic for update free pull ui.
    private void UpdateFreePullUI()
    {
        if (pull1CostText == null || _pull1CostCache == 0 || !_freePullStateLoaded) return;

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

    // Executes core business logic for is free pull available.
    // Returns a boolean indicating operation success.
    private bool IsFreePullAvailable()
    {
        return System.DateTime.Now >= GetNextFreePullTime();
    }

    // Executes core business logic for get next free pull time.
    private System.DateTime GetNextFreePullTime()
    {
        return _lastFreePullUtc == System.DateTime.MinValue
            ? System.DateTime.MinValue
            : _lastFreePullUtc.AddHours(24);
    }

    // Executes core business logic for use free pull.
    // Logic details: validates required non-empty string arguments.
    private void UseFreePull()
    {
        _previousFreePullUtc = _lastFreePullUtc;
        _lastFreePullUtc = System.DateTime.UtcNow;
    }

    // Executes core business logic for load free pull state.
    // Logic details: validates required non-empty string arguments.
    private void LoadFreePullState()
    {
        _freePullStateLoaded = false;
        _lastFreePullUtc = System.DateTime.MinValue;
        _previousFreePullUtc = System.DateTime.MinValue;

        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                if (profile != null && !string.IsNullOrWhiteSpace(profile.LastFreeGachaTime) &&
                    System.DateTime.TryParse(
                        profile.LastFreeGachaTime,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var lastFreeUtc))
                {
                    _lastFreePullUtc = lastFreeUtc;
                }

                _freePullStateLoaded = true;
                UpdateFreePullUI();
            },
            error =>
            {
                Debug.LogWarning($"[GachaUI] Failed to load free-pull state: {error.Message}");
                _freePullStateLoaded = true;
                UpdateFreePullUI();
            });
    }

    // Executes core business logic for perform pull.
    private void PerformPull(int amount)
    {
        bool isFreePull = false;
        if (amount == 1 && _freePullStateLoaded && IsFreePullAvailable())
        {
            isFreePull = true;
            UseFreePull();
        }

        SetButtonsInteractable(false);
        GachaApi.Instance.Pull(currentBannerId, amount, isFreePull,
            onSuccess: (result) =>
            {
                WorldRuntimeEvents.RaiseCurrencyChanged();
                VideoClip clipToPlay = ResolveVideoClip(amount);
                PlayGachaVideo(clipToPlay, () =>
                {
                    ShowResultPopup(result);
                    LoadUserTicketCount();
                    InventoryUIManager.RefreshAny(refreshStats: true);
                });
            },
            onError: (error) =>
            {
                Debug.LogWarning("[GachaUI] Pull failed: " + error.Message);

                if (isFreePull)
                {
                    _lastFreePullUtc = _previousFreePullUtc;
                    UpdateFreePullUI();
                }

                ShowWarningPopup("Not enough tickets or an error occurred!\n" + error.Message);
                SetButtonsInteractable(true);
            }
        );
    }

    // Executes core business logic for resolve video clip.
    private VideoClip ResolveVideoClip(int amount)
    {
        VideoClip clip = amount >= 10 ? videoClipX10 : videoClipX1;
        if (clip != null)
            return clip;

        string resourcePath = amount >= 10 ? "Videos/GachaX10" : "Videos/GachaX1";
        clip = Resources.Load<VideoClip>(resourcePath);
        if (clip == null)
            Debug.LogError($"[GachaUI] Missing Resources video at '{resourcePath}'. The gacha animation cannot play.");

        return clip;
    }

    // Executes core business logic for play gacha video.
    private void PlayGachaVideo(VideoClip clip, System.Action onComplete)
    {
        if (clip == null)
        {
            Debug.LogWarning("[GachaUI] VideoClip is null, skipping video animation.");
            onComplete?.Invoke();
            return;
        }

        EnsureVideoComponents();

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

    // Executes core business logic for on video loop point reached.
    private void OnVideoLoopPointReached(VideoPlayer vp)
    {
        FinishGachaVideo();
    }

    // Executes core business logic for skip gacha video.
    private void SkipGachaVideo()
    {
        FinishGachaVideo();
    }

    // Executes core business logic for finish gacha video.
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

    // Executes core business logic for ensure video components.
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
    // Executes core business logic for show warning popup.
    private void ShowWarningPopup(string message)
    {
        UIPopupBox.Notify(transform, "Gacha", message);
    }



    // Executes core business logic for show result popup.
    private void ShowResultPopup(MultiPullResultResponse result)
    {
        foreach (Transform child in resultItemContainer) Destroy(child.gameObject);

        int currentPity = 0;

        if (result.PulledItems != null)
        {
            var order = new List<GachaPullResultResponse>();
            var counts = new Dictionary<int, int>();

            foreach (var item in result.PulledItems)
            {
                if (item.PulledItemId > 0 && counts.ContainsKey(item.PulledItemId))
                {
                    counts[item.PulledItemId]++;
                }
                else
                {
                    if (item.PulledItemId > 0) counts[item.PulledItemId] = 1;
                    order.Add(item);
                }

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

                    if (ui.itemNameText != null)
                    {
                        ui.itemNameText.text = $"<color={hexColor}>{item.PulledItemName}</color>";
                    }

                    int count;
                    ui.SetQuantity(counts.TryGetValue(item.PulledItemId, out count) ? count : 1, hexColor);
                }
            }
        }

        UpdatePityUI(currentPity);
        resultPopupPanel.SetActive(true);
    }

    // Update visibility for result popup; it updates active and updates buttons interactable.
    private void CloseResultPopup()
    {
        resultPopupPanel.SetActive(false);
        SetButtonsInteractable(true);
    }

    // Executes core business logic for update pity ui.
    private void UpdatePityUI(int currentPity)
    {
        if (pityText == null) return;

        int pullsLeft = _pityLimit - currentPity;

        string colorTag = pullsLeft <= 10 ? "<color=red>" : "<color=yellow>";

        pityText.text = $"Guaranteed great item in: {colorTag}{pullsLeft}</color> pulls";
    }

    // Executes core business logic for set buttons interactable.
    // Logic details: validates required non-empty string arguments.
    private void SetButtonsInteractable(bool state)
    {
        if (btnPull1 != null) btnPull1.interactable = state;
        if (btnPull10 != null) btnPull10.interactable = state;
    }

    // Executes core business logic for get rarity color hex.
    // Logic details: validates required non-empty string arguments.
    private string GetRarityColorHex(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return "#C0C7D1";
        switch (rarity.Trim().ToLower())
        {
            case "mythic": return "#FF3340";
            case "legendary": return "#FFC726";
            case "epic": return "#B847FF";
            case "rare": return "#26A6FF";
            case "uncommon": return "#40E066";
            case "common":
            default: return "#C0C7D1";
        }
    }
}
