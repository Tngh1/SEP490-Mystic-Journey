using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;

namespace UI.Friend
{
    public class UINameChangePopup : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_InputField nameInput;
        public TMP_Text costText;
        public TMP_Text messageText;
        public Button confirmButton;
        public Button cancelButton;

        private Action<string> _onSuccess;
        private bool _isFree;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(ClosePopup);
        }

        public void ShowPopup(bool isFree, Action<string> onSuccess)
        {
            _isFree = isFree;
            _onSuccess = onSuccess;

            if (nameInput != null) nameInput.text = "";
            if (messageText != null) messageText.text = "";
            if (costText != null) costText.text = isFree ? "Cost: Free" : "Cost: 500 Gems";

            gameObject.SetActive(true);
        }

        public void ClosePopup()
        {
            gameObject.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            if (nameInput == null) return;

            string newName = nameInput.text.Trim();
            if (string.IsNullOrEmpty(newName) || newName.Length < 3 || newName.Length > 16)
            {
                if (messageText != null) messageText.text = "Name must be between 3 and 16 characters.";
                return;
            }

            if (confirmButton != null) confirmButton.interactable = false;
            if (messageText != null) messageText.text = "Processing...";

            var request = new ChangeNameRequestDto { NewName = newName };

            PlayerApi.Instance.ChangeName(request,
                response =>
                {
                    if (confirmButton != null) confirmButton.interactable = true;
                    _onSuccess?.Invoke(response.DisplayName);
                    ClosePopup();
                },
                error =>
                {
                    if (confirmButton != null) confirmButton.interactable = true;
                    if (messageText != null) messageText.text = error.Message;
                });
        }
    }
}
