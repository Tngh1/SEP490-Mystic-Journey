using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysticJourney.API.Models;
using System;

namespace MysticJourney.UI.Guild
{
    public class UIGuildEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txtGuildName;
        [SerializeField] private TextMeshProUGUI txtLevel;
        [SerializeField] private TextMeshProUGUI txtMemberCount;
        [SerializeField] private Button btnApply;
        [SerializeField] private Button btnEntry; // Có cái thì bỏ
        
        [Header("Rank Specific (Optional)")]
        [SerializeField] private TextMeshProUGUI txtRank;
        [SerializeField] private TextMeshProUGUI txtFeats;

        private int guildId;
        private Action<int> onApplyCallback;
        private Action<int> onEntryCallback;

        public void Setup(GuildResponseDto data, Action<int> entryClicked, Action<int> applyClicked, int rank = 0)
        {
            guildId = data.guildId;
            onEntryCallback = entryClicked;
            onApplyCallback = applyClicked;

            if (txtGuildName != null) txtGuildName.text = data.name;
            if (txtLevel != null) txtLevel.text = $"Lv. {data.level}";
            if (txtMemberCount != null) txtMemberCount.text = $"{data.memberCount}/{data.maxMembers}";
            
            if (txtRank != null) txtRank.text = rank > 0 ? rank.ToString() : "-";
            if (txtFeats != null) txtFeats.text = data.totalMedals.ToString();

            if (btnApply != null)
            {
                var txt = btnApply.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    if (data.isInvited)
                        txt.text = "Accept";
                    else
                        txt.text = data.joinPolicy == 0 ? "Join" : "Apply";
                }

                int playerLevel = UnityEngine.PlayerPrefs.GetInt("mj_player_level", 1);
                
                // Bypass level requirement if invited
                if (!data.isInvited && playerLevel < data.requiredLevel)
                {
                    btnApply.interactable = false;
                    if (txt != null) txt.text = $"Lv {data.requiredLevel}+";
                }
                else
                {
                    btnApply.interactable = true;
                }

                btnApply.onClick.RemoveAllListeners();
                btnApply.onClick.AddListener(() => onApplyCallback?.Invoke(guildId));
                
                // Highlight button color if invited
                var img = btnApply.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.color = data.isInvited ? new Color(0.2f, 0.8f, 0.2f, 1f) : Color.white; // Green if invited
                }
            }

            if (btnEntry != null)
            {
                btnEntry.onClick.RemoveAllListeners();
                btnEntry.onClick.AddListener(() => onEntryCallback?.Invoke(guildId));
            }
        }

        public void SetupRank(GuildRankResponseDto data, Action<int> entryClicked, Action<int> applyClicked)
        {
            guildId = data.guildId;
            onEntryCallback = entryClicked;
            onApplyCallback = applyClicked;

            if (txtRank != null) txtRank.text = data.rank.ToString();
            if (txtGuildName != null) txtGuildName.text = data.name;
            if (txtLevel != null) txtLevel.text = $"Lv. {data.level}";
            if (txtMemberCount != null) txtMemberCount.text = $"{data.memberCount}/{data.maxMembers}";
            if (txtFeats != null) txtFeats.text = data.totalFeats.ToString();

            // In rank view, we want to HIDE the Apply button
            if (btnApply != null)
            {
                btnApply.gameObject.SetActive(false);
            }

            if (btnEntry != null)
            {
                btnEntry.onClick.RemoveAllListeners();
                btnEntry.onClick.AddListener(() => onEntryCallback?.Invoke(guildId));
            }
        }
    }
}
