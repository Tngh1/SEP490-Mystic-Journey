using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;

namespace MysticJourney.UI.Guild
{
    public class UIGuildDonatePanel : MonoBehaviour
    {
        [Header("Selection Buttons (Gold)")]
        [SerializeField] private Button btnGold10k;
        [SerializeField] private Button btnGold100k;
        [SerializeField] private Button btnGoldMax;

        [Header("Selection Buttons (Gem)")]
        [SerializeField] private Button btnGem50;
        [SerializeField] private Button btnGem200;
        [SerializeField] private Button btnGemMax;

        [Header("Actions")]
        [SerializeField] private Button btnDonate;
        [SerializeField] private Button btnCancel;

        private int _currentGuildId;
        private Action _onDonateSuccess;
        
        private string _selectedCurrency = "Gold";
        private int _selectedAmount = 10000;
        private PlayerProfileResponse _currentProfile;

        private void Start()
        {
            // Binding selection buttons
            if (btnGold10k != null) btnGold10k.onClick.AddListener(() => SelectOption("Gold", 10000, btnGold10k));
            if (btnGold100k != null) btnGold100k.onClick.AddListener(() => SelectOption("Gold", 100000, btnGold100k));
            if (btnGoldMax != null) btnGoldMax.onClick.AddListener(() => SelectMax("Gold", btnGoldMax));

            if (btnGem50 != null) btnGem50.onClick.AddListener(() => SelectOption("Gem", 50, btnGem50));
            if (btnGem200 != null) btnGem200.onClick.AddListener(() => SelectOption("Gem", 200, btnGem200));
            if (btnGemMax != null) btnGemMax.onClick.AddListener(() => SelectMax("Gem", btnGemMax));

            // Binding action buttons
            if (btnDonate != null) btnDonate.onClick.AddListener(OnDonateClicked);
            if (btnCancel != null) btnCancel.onClick.AddListener(ClosePanel);
        }

        public void Open(int guildId, Action onSuccess = null)
        {
            _currentGuildId = guildId;
            _onDonateSuccess = onSuccess;
            gameObject.SetActive(true);

            // Default selection
            SelectOption("Gold", 10000, btnGold10k);

            // Fetch profile for MAX calculation
            PlayerApi.Instance.GetMyProfile(
                profile => _currentProfile = profile,
                error => Debug.LogError("Failed to fetch profile: " + error.Message)
            );
        }

        public void ClosePanel()
        {
            gameObject.SetActive(false);
        }

        private void SelectOption(string currency, int amount, Button clickedBtn)
        {
            _selectedCurrency = currency;
            _selectedAmount = amount;
            HighlightButton(clickedBtn);
        }

        private void SelectMax(string currency, Button clickedBtn)
        {
            if (_currentProfile == null)
            {
                Debug.LogWarning("Profile not loaded yet!");
                return;
            }

            _selectedCurrency = currency;
            if (currency == "Gold")
            {
                _selectedAmount = (int)_currentProfile.Gold;
            }
            else if (currency == "Gem")
            {
                _selectedAmount = (int)_currentProfile.Gems;
            }

            if (_selectedAmount <= 0) _selectedAmount = 1; // Fallback
            HighlightButton(clickedBtn);
        }

        private void HighlightButton(Button activeBtn)
        {
            Button[] allButtons = { btnGold10k, btnGold100k, btnGoldMax, btnGem50, btnGem200, btnGemMax };
            foreach (var btn in allButtons)
            {
                if (btn == null) continue;
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    Color c = img.color;
                    c.a = (btn == activeBtn) ? 1.0f : 0.5f; // Dim unselected
                    img.color = c;
                }
            }
        }

        private void OnDonateClicked()
        {
            if (MysticJourney.UI.UIPopupManager.Instance != null)
            {
                MysticJourney.UI.UIPopupManager.Instance.ShowConfirm(
                    "Confirm Donation",
                    $"Are you sure you want to donate {_selectedAmount} {_selectedCurrency}?",
                    ExecuteDonation,
                    null
                );
            }
            else
            {
                ExecuteDonation(); // Fallback
            }
        }

        private void ExecuteDonation()
        {
            GuildApi.Donate(_currentGuildId, _selectedCurrency, _selectedAmount,
                res =>
                {
                    Debug.Log($"[Donate] Success! Exp: {res.guildExpGained}, Feats: {res.playerFeatsGained}");
                    _onDonateSuccess?.Invoke();
                    ClosePanel();
                },
                err =>
                {
                    Debug.LogError("[Donate] Failed: " + err.Message);
                    if (MysticJourney.UI.UIPopupManager.Instance != null)
                        MysticJourney.UI.UIPopupManager.Instance.ShowAlert("Failed", err.Message);
                });
        }
    }
}
