using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using System.Collections.Generic;

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

        /// <summary>
        /// Gọi hàm này khi Boss chết hoặc màn chơi kết thúc
        /// </summary>
        public void ShowPanel(int sessionId)
        {
            currentSessionId = sessionId;
            gameObject.SetActive(true);
            
            // Xóa các item cũ (nếu có) trước khi hiển thị mới
            if (rewardContainer != null)
            {
                foreach (Transform child in rewardContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // Gọi API ClaimReward đã làm hôm qua
            DungeonApi.Instance.ClaimReward(sessionId, OnClaimSuccess, OnClaimError);
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
        }

        private void OnExitClicked()
        {
            gameObject.SetActive(false);
            
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.ReturnToWorldMap();
            }
        }

        private void OnAgainClicked()
        {
            if (DungeonManager.Instance != null)
            {
                // Let DungeonManager handle it. The panel will be closed if restart succeeds (scene reload),
                // but if it fails (e.g. not enough energy), the panel stays open so they can buy energy or exit.
                DungeonManager.Instance.RestartDungeon();
            }
        }
    }
}
