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
        [SerializeField] private Button btnEntry; // Cả cái thẻ bự

        private int guildId;
        private Action<int> onApplyCallback;
        private Action<int> onEntryCallback;

        public void Setup(GuildResponseDto data, Action<int> entryClicked, Action<int> applyClicked)
        {
            guildId = data.guildId;
            onEntryCallback = entryClicked;
            onApplyCallback = applyClicked;

            if (txtGuildName != null) txtGuildName.text = data.name;
            if (txtLevel != null) txtLevel.text = $"Lv. {data.level}";
            if (txtMemberCount != null) txtMemberCount.text = $"{data.memberCount}/{data.maxMembers}";

            if (btnApply != null)
            {
                var txt = btnApply.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.text = data.joinPolicy == 0 ? "Join" : "Apply";
                }

                int playerLevel = UnityEngine.PlayerPrefs.GetInt("mj_player_level", 1);
                if (playerLevel < data.requiredLevel)
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
            }

            if (btnEntry != null)
            {
                btnEntry.onClick.RemoveAllListeners();
                btnEntry.onClick.AddListener(() => onEntryCallback?.Invoke(guildId));
            }
        }
    }
}
