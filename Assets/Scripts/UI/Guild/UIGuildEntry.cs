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
