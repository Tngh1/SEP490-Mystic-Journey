using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MysticJourney.UI
{
    // Executes core business logic for mono behaviour.
    public class UIPopupManager : MonoBehaviour
    {
        private static UIPopupManager _instance;
        // Executes core business logic for instance.
        public static UIPopupManager Instance
        {
            get
            {
                if (_instance == null)
                {
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

        // Executes core business logic for popup container.
        public GameObject PopupContainer => popupContainer;
        // Executes core business logic for btn confirm.
        public Button BtnConfirm => btnConfirm;

        private Action onConfirmAction;
        private Action onCancelAction;

        // Initializes internal component caches and dependencies for UIPopupManager upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirmClicked);
            if (btnCancel != null) btnCancel.onClick.AddListener(OnCancelClicked);
            if (btnBackgroundBlocker != null) btnBackgroundBlocker.onClick.AddListener(OnCancelClicked);

            if (btnBackgroundBlocker != null) btnBackgroundBlocker.gameObject.SetActive(false);

            if (popupContainer != null) popupContainer.SetActive(false);
        }

        // Executes core business logic for show alert.
        public void ShowAlert(string title, string message, Action onOk = null, string okText = "OK", bool autoClose = true)
        {
            _autoClose = autoClose;
            SetupPopup(title, message, okText, "", onOk, null, showCancelButton: false);
        }

        // Executes core business logic for show confirm.
        public void ShowConfirm(string title, string message, Action onConfirm, Action onCancel = null, string confirmText = "Yes", string cancelText = "No", bool autoClose = true)
        {
            _autoClose = autoClose;
            SetupPopup(title, message, confirmText, cancelText, onConfirm, onCancel, showCancelButton: true);
        }

        private bool _autoClose = true;

        // Executes core business logic for setup popup.
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

            Transform current = transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
                current = current.parent;
            }

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

        // Executes core business logic for hide popup.
        public void HidePopup()
        {
            if (popupContainer != null) popupContainer.SetActive(false);
            if (btnBackgroundBlocker != null) btnBackgroundBlocker.gameObject.SetActive(false);
            onConfirmAction = null;
            onCancelAction = null;
        }

        // Executes core business logic for on confirm clicked.
        private void OnConfirmClicked()
        {
            if (_autoClose)
            {
                if (popupContainer != null) popupContainer.SetActive(false);
                if (btnBackgroundBlocker != null) btnBackgroundBlocker.gameObject.SetActive(false);
            }
            onConfirmAction?.Invoke();
        }

        // Executes core business logic for on cancel clicked.
        private void OnCancelClicked()
        {
            if (_autoClose)
            {
                if (popupContainer != null) popupContainer.SetActive(false);
                if (btnBackgroundBlocker != null) btnBackgroundBlocker.gameObject.SetActive(false);
            }
            onCancelAction?.Invoke();
        }
    }
}
