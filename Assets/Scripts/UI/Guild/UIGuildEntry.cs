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

        private int guildId;
        private Action<int> onApplyClicked;

        public void Setup(GuildResponseDto data, Action<int> applyCallback)
        {
            guildId = data.guildId;
            onApplyClicked = applyCallback;

            if (txtGuildName != null) txtGuildName.text = data.name;
            if (txtLevel != null) txtLevel.text = $"Lv. {data.level}";
            if (txtMemberCount != null) txtMemberCount.text = $"{data.memberCount}/{data.maxMembers}";

            if (btnApply != null)
            {
                btnApply.onClick.RemoveAllListeners();
                btnApply.onClick.AddListener(OnApplyBtnClicked);
            }
        }

        private void OnApplyBtnClicked()
        {
            onApplyClicked?.Invoke(guildId);
        }
    }
}
