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
    public class UIDungeonCompletePanel : MonoBehaviour
    {
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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
            if (againButton != null) againButton.onClick.AddListener(OnAgainClicked);
        }

        private void OnEnable()
        {
            NetworkPlayer.OnAnyReadyStateChanged += UpdateReadyState;
        }

        private void OnDisable()
        {
            NetworkPlayer.OnAnyReadyStateChanged -= UpdateReadyState;
        }

        /// <summary>
        /// Gọi hàm này khi Boss chết hoặc màn chơi kết thúc
        /// </summary>
        public void ShowPanel(int sessionId)
        {
            currentSessionId = sessionId;
            gameObject.SetActive(true);

            // Reset Again button
            if (againButton != null)
            {
                againButton.interactable = true;
                var txt = againButton.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = "Again";
            }
            
            // Xóa các item cũ (nếu có) trước khi hiển thị mới
            if (rewardContainer != null)
            {
                foreach (Transform child in rewardContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // Clear placeholder texts
            if (goldText != null) goldText.text = "...";
            if (expText != null) expText.text = "...";

            if (timeText != null) timeText.text = "--:--";

            StartCoroutine(ClaimWithRetry(sessionId));
        }

        /// <summary>
        /// The backend only allows claim-reward once the session is "Completed", and only the
        /// host marks it so (POST complete). The host broadcasts RPC_BossDied *before* that call
        /// returns, so a party member who reaches the chest first used to get
        /// "cannot have rewards claimed (status: InProgress)" and the panel sat on "..." with no
        /// gold, no items and no time. Retry briefly instead of failing the whole reward.
        /// </summary>
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
                        // Only the "not completed yet" race is worth retrying; a duplicate claim
                        // or a missing session will never succeed on a second attempt.
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

        private void OnClaimSuccess(ClaimDungeonRewardResponse response)
        {
            Debug.Log("Claim Reward Success!");

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

        private void OnClaimError(ApiException error)
        {
            Debug.LogError($"Claim Reward Failed: {error.Message}");

            // Never leave the panel on "..." — that reads as "still loading" forever. A failed
            // claim (already claimed, session gone) still has to resolve to something visible.
            if (goldText != null) goldText.text = "+0";
            if (expText != null) expText.text = "+0";

            // Năng lượng được kiểm tra & trừ ở đây (claim-reward), KHÔNG chặn lúc vào dungeon.
            // Nếu không đủ thì phải nói rõ, nếu không người chơi chỉ thấy +0/+0 và tưởng bug.
            // Khớp theo message giống ClaimWithRetry ở trên: ErrorCode là INVALID_OPERATION dùng
            // chung cho mọi lỗi claim nên không phân biệt được trường hợp thiếu năng lượng.
            if (error.Message != null && error.Message.Contains("Insufficient energy"))
            {
                UIPopupBox.Notify(
                    transform,
                    "Not Enough Energy",
                    "You don't have enough energy to claim this chest.\n\n" +
                    "Energy regenerates over time — come back and clear the dungeon again once it has refilled.");
            }
        }

        private void OnExitClicked()
        {
            if (exitButton != null) exitButton.interactable = false;

            // Leaving cancels our restart vote. Without this, the flag stayed true on our
            // replicated avatar for the moment before it despawns and the host could count a
            // departing player as ready.
            if (NetworkPlayer.Local != null) NetworkPlayer.Local.RPC_ClearReadyToRestart();

            gameObject.SetActive(false);

            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.ReturnToWorldMap();
            }
        }

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
                // Offline / single-player has no NetworkPlayer at all, so the ready-vote path
                // never resolves and the button just went dead. Restart straight away.
                DungeonManager.Instance?.RestartDungeon();
            }
        }

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
                // The host clears every ready flag when it fires the restart, and also when a
                // vote is abandoned (someone exited). Without this the button stayed disabled
                // reading "Waiting..." forever with nothing left to wait for.
                txt.text = "Again";
                againButton.interactable = true;
            }
        }
    }
}
