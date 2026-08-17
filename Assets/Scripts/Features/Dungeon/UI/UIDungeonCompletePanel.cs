using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace MysticJourney.Features.Dungeon.UI
{
    // Executes mono behaviour operation.
    public class UIDungeonCompletePanel : MonoBehaviour
    {
        // Executes instance operation.
        public static UIDungeonCompletePanel Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI expText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private Transform rewardContainer;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button againButton;

        [Header("Optional Prefab")]
        [Tooltip("Prefab để hiển thị các Item/Equipment (nếu có)")]
        [SerializeField] private GameObject rewardItemPrefab;

        private int currentSessionId;

        // Initializes internal component caches and dependencies for UIDungeonCompletePanel upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
            if (againButton != null) againButton.onClick.AddListener(OnAgainClicked);
        }

        // Refresh visible state and subscribe the event handlers required while this component is active.
        private void OnEnable()
        {
            NetworkPlayer.OnAnyReadyStateChanged += UpdateReadyState;
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDisable()
        {
            NetworkPlayer.OnAnyReadyStateChanged -= UpdateReadyState;
        }

        // Executes show panel operation.
        public void ShowPanel(int sessionId)
        {
            currentSessionId = sessionId;
            gameObject.SetActive(true);

            if (exitButton != null) exitButton.interactable = true;

            if (againButton != null)
            {
                againButton.interactable = true;
                var txt = againButton.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = "Again";
            }

            if (rewardContainer != null)
            {
                foreach (Transform child in rewardContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            if (goldText != null) goldText.text = "...";
            if (expText != null) expText.text = "...";

            if (timeText != null) timeText.text = "--:--";

            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(ClaimWithRetry(sessionId));
        }

        // Executes claim with retry operation.
        private IEnumerator ClaimWithRetry(int sessionId)
        {
            if (sessionId <= 0)
            {
                Debug.LogWarning("[UIDungeonCompletePanel] Session ID is invalid (offline fallback). Skipping claim API.");
                if (goldText != null) goldText.text = "+0";
                if (expText != null) expText.text = "+0";
                yield break;
            }

            const int maxAttempts = 6;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                bool done = false;
                bool retryable = false;

                DungeonApi.Instance.ClaimReward(
                    sessionId,
                    response => { OnClaimSuccess(response); done = true; },
                    error =>
                    {
                        retryable = error.Message != null && error.Message.Contains("Complete the dungeon first");
                        if (!retryable) OnClaimError(error);
                        done = true;
                    });

                yield return new WaitUntil(() => done);

                if (!retryable) yield break;

                Debug.Log($"[UIDungeonCompletePanel] Session not marked complete yet, retrying claim ({attempt}/{maxAttempts})...");
                yield return new WaitForSeconds(0.5f);
            }

            Debug.LogWarning("[UIDungeonCompletePanel] Claim reward timed out waiting for the host to complete the session.");
            if (goldText != null) goldText.text = "+0";
            if (expText != null) expText.text = "+0";
        }

        // Executes on claim success operation.
        private void OnClaimSuccess(ClaimDungeonRewardResponse response)
        {
            Debug.Log("Claim Reward Success!");

            if (response.Wallet != null)
            {
                PlayerHUDUIManager.Instance?.ApplyCurrencyBalance(new CurrencyBalanceResponse
                {
                    Gold = response.Wallet.Gold,
                    Gems = response.Wallet.Gems
                });
            }

            if (response.Character != null)
            {
                PlayerHUDUIManager.Instance?.ApplyEnergy(
                    response.Character.Energy,
                    response.Character.MaxEnergy);
            }

            WorldRuntimeEvents.RaiseCurrencyChanged();

            if (goldText != null)
                goldText.text = "+" + response.GoldEarned.ToString();

            if (expText != null)
                expText.text = "+" + response.ExperienceEarned.ToString();

            if (timeText != null)
            {
                int minutes = Mathf.FloorToInt(response.TimeTakenSeconds / 60f);
                int seconds = Mathf.FloorToInt(response.TimeTakenSeconds % 60f);
                timeText.text = $"{minutes:00}:{seconds:00}";
            }

            if (response.Items != null && rewardItemPrefab != null)
            {
                foreach (var item in response.Items)
                {
                    var go = Instantiate(rewardItemPrefab, rewardContainer);
                    // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
                    var slot = go.GetComponent<UIRewardSlot>();
                    if (slot != null)
                    {
                        var data = new UIItemDisplayData
                        {
                            itemId = item.ItemId,
                            itemName = item.ItemName,
                            quantity = item.Quantity,
                            rarity = item.Rarity,
                            icon = ItemIconDatabase.Instance != null ? ItemIconDatabase.Instance.GetIcon(item.ItemName, item.ItemType) : null
                        };
                        slot.SetupReward(data);
                    }
                }
            }
        }

        // Executes on claim error operation.
        private void OnClaimError(ApiException error)
        {
            Debug.LogError($"Claim Reward Failed: {error.Message}");

            if (goldText != null) goldText.text = "+0";
            if (expText != null) expText.text = "+0";

            if (error.Message != null && error.Message.Contains("Insufficient energy"))
            {
                UIPopupBox.Notify(
                    transform,
                    "Not Enough Energy",
                    "You don't have enough energy to claim this chest.\n\n" +
                    "Energy regenerates over time — come back and clear the dungeon again once it has refilled.");
            }
        }

        // Executes on exit clicked operation.
        private void OnExitClicked()
        {
            if (exitButton != null) exitButton.interactable = false;

            gameObject.SetActive(false);

            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.ReturnToWorldMap();
            }

            NetworkPlayer.Local?.CancelRestartVoteForExit();
        }

        // Executes on again clicked operation.
        private void OnAgainClicked()
        {
            if (againButton != null)
            {
                againButton.interactable = false;
            }

            if (NetworkPlayer.Local != null)
            {
                NetworkPlayer.Local.RPC_SetReadyToRestart();
            }
            else
            {
                DungeonManager.Instance?.RestartDungeon();
            }
        }

        // Executes update ready state operation.
        private void UpdateReadyState()
        {
            if (!gameObject.activeInHierarchy || againButton == null) return;

            int readyCount = NetworkPlayer.All.Count(p => p.IsReadyToRestart);
            int totalCount = NetworkPlayer.All.Count;

            var txt = againButton.GetComponentInChildren<TMP_Text>();
            if (txt == null) return;

            if (readyCount > 0)
            {
                txt.text = $"Waiting... ({readyCount}/{totalCount})";
            }
            else
            {
                txt.text = "Again";
                againButton.interactable = true;
            }
        }
    }
}
