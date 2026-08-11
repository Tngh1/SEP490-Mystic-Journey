using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MysticJourney.UI
{
    public class UIPopupManager : MonoBehaviour
    {
        private static UIPopupManager _instance;
        public static UIPopupManager Instance 
        { 
            get
            {
                if (_instance == null)
                {
                    // Tìm kiếm cả object đang bị tắt (inactive)
                    _instance = FindFirstObjectByType<UIPopupManager>(FindObjectsInactive.Include);
                    if (_instance != null && !_instance.gameObject.activeInHierarchy)
                    {
                        _instance.gameObject.SetActive(true);
                    }
                }
                return _instance;
            }
        }

        [Header("Main Container")]
        [SerializeField] private GameObject popupContainer;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI txtTitle;
        [SerializeField] private TextMeshProUGUI txtMessage;

        [Header("Buttons")]
        [SerializeField] private Button btnConfirm;
        [SerializeField] private TextMeshProUGUI txtConfirmLabel;
        
        [SerializeField] private Button btnCancel;
        [SerializeField] private TextMeshProUGUI txtCancelLabel;

        [Header("Background Blocker")]
        [SerializeField] private Button btnBackgroundBlocker;

        private Action onConfirmAction;
        private Action onCancelAction;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Bind events
            if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirmClicked);
            if (btnCancel != null) btnCancel.onClick.AddListener(OnCancelClicked);
            if (btnBackgroundBlocker != null) btnBackgroundBlocker.onClick.AddListener(OnCancelClicked); // Click ra ngoài cũng tính là Cancel

            // Reset cả hai về ẩn. Trong scene BackgroundBlocker được serialize là active=true,
            // nên nếu không tắt ở đây thì chỉ cần một chỗ *đọc* UIPopupManager.Instance (getter
            // tự SetActive(true) lên GameObject này) đúng lúc PopupLayer đang bật là màn hình có
            // ngay một Button trong suốt phủ kín, không popup nào để bấm OK => treo cả UI.
            // Kiểu null-check `if (UIPopupManager.Instance != null)` đó nằm rải rác ở
            // UIFriendPanel, GuildUIManager...
            if (btnBackgroundBlocker != null) btnBackgroundBlocker.gameObject.SetActive(false);

            if (popupContainer != null) popupContainer.SetActive(false);
        }

        /// <summary>
        /// Hiện bảng thông báo (chỉ có nút OK). 
        /// </summary>
        public void ShowAlert(string title, string message, Action onOk = null, string okText = "OK")
        {
            SetupPopup(title, message, okText, "", onOk, null, showCancelButton: false);
        }

        /// <summary>
        /// Hiện bảng xác nhận (có nút Yes/No).
        /// </summary>
        public void ShowConfirm(string title, string message, Action onConfirm, Action onCancel = null, string confirmText = "Yes", string cancelText = "No")
        {
            SetupPopup(title, message, confirmText, cancelText, onConfirm, onCancel, showCancelButton: true);
        }

        private void SetupPopup(string title, string message, string confirmText, string cancelText, Action onConfirm, Action onCancel, bool showCancelButton)
        {
            onConfirmAction = onConfirm;
            onCancelAction = onCancel;

            if (txtTitle != null) txtTitle.text = title;
            if (txtMessage != null) txtMessage.text = message;

            if (txtConfirmLabel != null) txtConfirmLabel.text = confirmText;
            if (txtCancelLabel != null) txtCancelLabel.text = cancelText;

            if (btnCancel != null)
            {
                btnCancel.gameObject.SetActive(showCancelButton);
            }

            // Bật lại mọi cấp cha đang tắt. PopupLayer là container DÙNG CHUNG cho 14 popup và bị
            // code khác tắt/bật (MainQuestPanelRuntime tắt nó sau khi chạy hết queue PaperPopup),
            // nên không được coi là luôn bật.
            // PHẢI walk TRƯỚC khi bật popupContainer/blocker: trong scene cả PopupLayer lẫn
            // GameObject này đều đang tắt, nên Awake() chưa hề chạy — Unity chỉ chạy nó đúng vào
            // lúc SetActive(true) ở vòng lặp dưới. Awake() kết thúc bằng việc tắt cả container lẫn
            // blocker, nên nếu bật chúng trước thì Awake() tắt lại ngay và popup không bao giờ hiện.
            Transform current = transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
                current = current.parent;
            }

            // Ép Canvas của UIPopupManager đè lên tầng cao nhất (SortingOrder = 9999)
            Canvas managerCanvas = GetComponentInParent<Canvas>();
            if (managerCanvas == null) managerCanvas = gameObject.AddComponent<Canvas>();
            if (managerCanvas != null)
            {
                managerCanvas.overrideSorting = true;
                managerCanvas.sortingOrder = 9999;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (btnBackgroundBlocker != null)
            {
                btnBackgroundBlocker.gameObject.SetActive(true);
            }

            if (popupContainer != null)
            {
                popupContainer.SetActive(true);
                popupContainer.transform.SetAsLastSibling();
            }
        }

        public void HidePopup()
        {
            if (popupContainer != null) popupContainer.SetActive(false);
            if (btnBackgroundBlocker != null) btnBackgroundBlocker.gameObject.SetActive(false);
            onConfirmAction = null;
            onCancelAction = null;
        }

        private void OnConfirmClicked()
        {
            if (popupContainer != null) popupContainer.SetActive(false);
            if (btnBackgroundBlocker != null) btnBackgroundBlocker.gameObject.SetActive(false);
            // Không nên tắt cả gameObject nếu UIPopupManager nằm chung Canvas với UI khác
            // gameObject.SetActive(false); 
            onConfirmAction?.Invoke();
        }

        private void OnCancelClicked()
        {
            if (popupContainer != null) popupContainer.SetActive(false);
            if (btnBackgroundBlocker != null) btnBackgroundBlocker.gameObject.SetActive(false);
            // gameObject.SetActive(false);
            onCancelAction?.Invoke();
        }
    }
}
