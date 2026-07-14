using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models
{
    [Serializable]
    public class GuildResponseDto
    {
        public int guildId;
        public string name;
        public string description;
        public string notice;
        public int iconId;
        public int bannerId;
        public int leaderId;
        public string leaderName;
        public int level;
        public int guildExp;
        public int expToNextLevel;
        public int medalsToNextLevel;
        public int memberCount;
        public int maxMembers;
        public int requiredLevel;
        public int joinPolicy;
        public int totalMedals;
        public bool isActive;
        public string createdAt;
    }

    [Serializable]
    public class GuildDetailResponseDto : GuildResponseDto
    {
        public List<GuildMemberResponseDto> members = new List<GuildMemberResponseDto>();
    }

    [Serializable]
    public class CreateGuildRequestDto
    {
        public string name;
        public string notice;
        public int requiredLevel;
        public int joinPolicy;
        public int iconId;
        public int bannerId;
    }

    [Serializable]
    public class ChangeNoticeRequest
    {
        public string notice;
    }

    [Serializable]
    public class ChangeIconRequest
    {
        public int iconId;
        public int? bannerId;
    }

    [Serializable]
    public class GuildMemberResponseDto
    {
        public int guildMemberId;
        public int guildId;
        public string guildName;
        public int playerProfileId;
        public string playerDisplayName;
        public string playerAvatarUrl;
        public int playerLevel;
        public string role; // "Leader", "Officer", "Member"
        public int medals;
        public int feats;
        public int dailyContribution;
        public int weeklyContribution;
        public int totalContribution;
        public bool isOnline;
        public string joinedAt;
        public string leftAt;
    }

    [Serializable]
    public class PromoteMemberRequest
    {
        public int targetPlayerProfileId;
    }

    [Serializable]
    public class TransferLeaderRequest
    {
        public int newLeaderProfileId;
    }

    [Serializable]
    public class InvitePlayerRequest
    {
        public int inviteeProfileId;
    }

    [Serializable]
    public class GuildJoinResultDto
    {
        public bool success;
        public bool canJoin;
        public int cooldownRemainingSeconds;
        public string message;
    }

    [Serializable]
    public class GuildApplicationDTO
    {
        public int guildApplicationId;
        public int playerProfileId;
        public string playerName;
        public string playerAvatarUrl;
        public int playerLevel;
        public int medals;
        public int feats;
        public string status;
        public string createdAt;
    }

    [Serializable]
    public class DonateRequest
    {
        public int amount;
    }

    [Serializable]
    public class GuildDonateResultDto
    {
        public int goldSpent;
        public int guildExpGained;
        public int guildMedalsGained;
        public int playerMedalsGained;
        public int playerFeatsGained;
        public bool guildLeveledUp;
        public int newGuildLevel;
        public int newGuildExp;
        public int expToNextLevel;
        public int totalMedals;
        public int medalsToNextLevel;
    }

    [Serializable]
    public class GuildMessageDTO
    {
        public int messageId;
        public int senderId;
        public string senderName;
        public string content;
        public int messageType; // 0=Text, 1=System, 2=Join, 3=Leave, 4=Promotion
        public int senderRole; // 0=Member, 1=Officer, 2=Leader
        public string sentAt;
    }

    [Serializable]
    public class SendGuildMessageRequest
    {
        public string content;
    }

    [Serializable]
    public class GuildLogDto
    {
        public int guildLogId;
        public string action;
        public string actorName;
        public string targetName;
        public string detail;
        public string createdAt;
    }
}
