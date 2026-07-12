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
                    _instance = FindObjectOfType<UIPopupManager>(true);
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

            if (btnBackgroundBlocker != null)
            {
                btnBackgroundBlocker.gameObject.SetActive(true);
            }

            if (popupContainer != null)
            {
                popupContainer.SetActive(true);
                popupContainer.transform.SetAsLastSibling();
            }

            // Đảm bảo GameObject chứa script này cũng đang bật
            if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
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
