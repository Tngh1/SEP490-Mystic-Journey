using UnityEngine;
using UnityEngine.UI;
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
    [Tooltip("Số dòng lịch sử hiển thị trên mỗi trang")]
    public int historyPageSize = 5;

    [Header("--- Rarity Icons ---")]
    public Sprite iconCommon;
    public Sprite iconUncommon;
    public Sprite iconRare;
    public Sprite iconEpic;
    public Sprite iconLegendary;
    public Sprite iconMythic;

    private int _historyPage = 1;
    private int _historyTotalPages = 1;
    private bool _isLoadingHistory;

    // 👇 KHU VỰC MỚI BỔ SUNG: WARNING POPUP
    [Header("--- Warning Popup ---")]
    public GameObject warningPopupPanel;
    public TextMeshProUGUI warningMessageText;
    public Button btnCloseWarning;

    [Header("--- Free Pull ---")]
    public TextMeshProUGUI freeCountdownText;
    private const string LastFreePullKey = "LastFreePullTime";
    private int _pull1CostCache = 0;

    private int _pityLimit = 90;
    private List<GachaBannerItemResponse> _cachedBannerItems = new List<GachaBannerItemResponse>();

    private void OnEnable()
    {
        btnPull1.onClick.AddListener(() => PerformPull(1));
        btnPull10.onClick.AddListener(() => PerformPull(10));
        if (btnCloseResult != null) btnCloseResult.onClick.AddListener(CloseResultPopup);

        if (btnOpenDetail != null) btnOpenDetail.onClick.AddListener(OpenDetailPanel);
        if (btnCloseDetail != null) btnCloseDetail.onClick.AddListener(() => detailPanel.SetActive(false));

        if (btnOpenHistory != null) btnOpenHistory.onClick.AddListener(OpenHistoryPanel);
        if (btnCloseHistory != null) btnCloseHistory.onClick.AddListener(() => historyPanel.SetActive(false));

        if (btnPrevPage != null) btnPrevPage.onClick.AddListener(() => ChangeHistoryPage(-1));
        if (btnNextPage != null) btnNextPage.onClick.AddListener(() => ChangeHistoryPage(1));

        // Bật lắng nghe nút đóng cảnh báo
        if (btnCloseWarning != null) btnCloseWarning.onClick.AddListener(CloseWarningPopup);

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
        btnPull1.onClick.RemoveAllListeners();
        btnPull10.onClick.RemoveAllListeners();
        if (btnCloseResult != null) btnCloseResult.onClick.RemoveAllListeners();
        if (btnOpenDetail != null) btnOpenDetail.onClick.RemoveAllListeners();
        if (btnCloseDetail != null) btnCloseDetail.onClick.RemoveAllListeners();
        if (btnOpenHistory != null) btnOpenHistory.onClick.RemoveAllListeners();
        if (btnCloseHistory != null) btnCloseHistory.onClick.RemoveAllListeners();
        if (btnPrevPage != null) btnPrevPage.onClick.RemoveAllListeners();
        if (btnNextPage != null) btnNextPage.onClick.RemoveAllListeners();
        if (btnCloseWarning != null) btnCloseWarning.onClick.RemoveAllListeners();
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
            onError: (error) => { Debug.LogError("[GachaUI] Lỗi tải Banner: " + error.Message); }
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
                Debug.LogWarning("[GachaUI] Không lấy được số vé quay: " + error.Message);
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
        if (detailPanel == null || detailItemContainer == null || detailItemPrefab == null) return;
        foreach (Transform child in detailItemContainer) Destroy(child.gameObject);

        foreach (var item in _cachedBannerItems)
        {
            GameObject go = Instantiate(detailItemPrefab, detailItemContainer);
            TextMeshProUGUI txt = go.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                string rarityColor = GetRarityColorHex(item.ItemRarity);
                txt.text = $"<color={rarityColor}>[{item.ItemRarity}] {item.ItemName}</color> - Tỉ lệ: <color=orange>{item.DropRate}%</color>";
            }
        }
        detailPanel.SetActive(true);
    }

    private void OpenHistoryPanel()
    {
        if (historyPanel == null || historyItemContainer == null || historyItemPrefab == null)
        {
            Debug.LogWarning("[GachaUI] HistoryPanel chưa được gán đủ (historyItemContainer/historyItemPrefab).");
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
                    Debug.LogWarning("[GachaUI] GetHistory trả về dữ liệu rỗng.");
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
                            txt.text = $"[{history.PulledAt.ToLocalTime():dd/MM HH:mm}] Quay ra: <color={rarityColor}>{history.RewardItemName}</color>";
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
                Debug.LogError("[GachaUI] Lỗi tải lịch sử: " + error.Message);
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
                Debug.LogWarning("[GachaUI] Không lấy được pity từ lịch sử: " + error.Message);
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
                ShowResultPopup(result);
                LoadUserTicketCount();
                InventoryManager.RefreshAny(refreshStats: false);
            },
            onError: (error) =>
            {
                Debug.LogWarning("[GachaUI] Quay thất bại: " + error.Message);

                // Nếu có lỗi, hoàn lại lượt free (tuỳ logic, tạm thời có thể hoàn lại nếu server từ chối)
                if (isFreePull)
                {
                    PlayerPrefs.DeleteKey(LastFreePullKey);
                    PlayerPrefs.Save();
                }

                // 👇 HIỂN THỊ POPUP NẾU QUAY LỖI (Ví dụ: Server báo không đủ vé)
                ShowWarningPopup("Không đủ vé quay hoặc có lỗi xảy ra!\n" + error.Message);

                SetButtonsInteractable(true);
            }
        );
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
            foreach (var item in result.PulledItems)
            {
                GameObject newItemUI = Instantiate(pulledItemPrefab, resultItemContainer);
                TextMeshProUGUI itemNameText = newItemUI.GetComponentInChildren<TextMeshProUGUI>();
                if (itemNameText != null)
                {
                    string hexColor = GetRarityColorHex(item.PulledItemRarity);
                    itemNameText.text = $"<color={hexColor}>{item.PulledItemName}</color>";
                }

                Image itemImage = newItemUI.GetComponentInChildren<Image>(true);
                if (itemImage != null)
                {
                    Sprite icon = null;
                    if (ItemIconDatabase.Instance != null)
                    {
                        icon = ItemIconDatabase.Instance.GetIcon(item.PulledItemName, item.PulledItemRarity);
                    }

                    if (icon == null)
                    {
                        icon = Resources.Load<Sprite>($"Icons/{item.PulledItemName}") ?? Resources.Load<Sprite>($"Icons/{item.PulledItemRarity}");
                    }

                    if (icon != null)
                    {
                        itemImage.sprite = icon;
                        itemImage.enabled = true;
                    }
                }

                // Ưu tiên dùng pity trả về từ backend
                if (item.CurrentPity >= 0)
                {
                    currentPity = item.CurrentPity;
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

        pityText.text = $"Chắc chắn nhận vật phẩm quý sau: {colorTag}{pullsLeft}</color> lượt";
    }

    private void SetButtonsInteractable(bool state)
    {
        if (btnPull1 != null) btnPull1.interactable = state;
        if (btnPull10 != null) btnPull10.interactable = state;
    }

    private string GetRarityColorHex(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return "#FFFFFF";
        switch (rarity.ToLower())
        {
            case "legendary": return "#FF4500";
            case "mythic": return "#FFD700";
            case "epic": return "#A020F0";
            case "rare": return "#0000FF";
            default: return "#FFFFFF";
        }
    }
}