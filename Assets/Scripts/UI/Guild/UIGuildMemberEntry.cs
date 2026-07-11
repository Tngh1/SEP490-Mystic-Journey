using UnityEngine;
using TMPro;
using MysticJourney.API.Models;

namespace MysticJourney.UI.Guild
{
    public class UIGuildMemberEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txtMemberName;
        [SerializeField] private TextMeshProUGUI txtLevel;
        [SerializeField] private TextMeshProUGUI txtMedals;
        [SerializeField] private TextMeshProUGUI txtFeats;
        [SerializeField] private TextMeshProUGUI txtStatus;
        
        // Có thể thêm Nút Kick/Promote ở đây nếu cần

        public void Setup(GuildMemberResponseDto member)
        {
            if (txtMemberName != null) txtMemberName.text = member.playerDisplayName;
            if (txtLevel != null) txtLevel.text = $"Lv. {member.playerLevel}";
            
            if (txtMedals != null) txtMedals.text = member.medals.ToString();
            if (txtFeats != null) txtFeats.text = member.feats.ToString();
            
            if (txtStatus != null)
            {
                txtStatus.text = member.isOnline ? "Online" : "Offline";
                txtStatus.color = member.isOnline ? Color.green : Color.gray;
            }
        }
    }
}
