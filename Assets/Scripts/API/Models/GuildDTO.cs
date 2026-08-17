using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models
{
    // Initializes a new default instance of the GuildResponseDto class.
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
        public string leaderAvatarUrl;
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
        public bool isInvited;
        public string createdAt;
    }

    // Initializes a new default instance of the GuildResponseDto class.
    [Serializable]
    public class GuildDetailResponseDto : GuildResponseDto
    {
        public List<GuildMemberResponseDto> members = new List<GuildMemberResponseDto>();
    }

    // Executes create guild request dto operation.
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

    // Executes change notice request operation.
    [Serializable]
    public class ChangeNoticeRequest
    {
        public string notice;
    }

    // Executes change icon request operation.
    [Serializable]
    public class ChangeIconRequest
    {
        public int iconId;
        public int? bannerId;
    }

    // Executes guild member response dto operation.
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
        // Supported guild roles: Member, Officer, or Leader; the role determines guild-management permissions.
        public string role;
        public int medals;
        public int feats;
        public int dailyContribution;
        public int weeklyContribution;
        public int totalContribution;
        public bool isOnline;
        public string joinedAt;
        public string leftAt;
        public string lastDonateAt;
    }

    // Executes transfer leader request operation.
    [Serializable]
    public class TransferLeaderRequest
    {
        public int newLeaderProfileId;
    }

    // Executes invite player request operation.
    [Serializable]
    public class InvitePlayerRequest
    {
        public int inviteeProfileId;
    }

    // Executes guild join result dto operation.
    [Serializable]
    public class GuildJoinResultDto
    {
        public bool success;
        public bool canJoin;
        public int cooldownRemainingSeconds;
        public string message;
    }

    // Executes guild application dto operation.
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
        // Supported guild request states: Pending, Accepted, Declined, or Expired; only Pending requests can transition to a final state.
        public string status;
        public string createdAt;
    }

    // Executes donate request operation.
    public class DonateRequest
    {
        // Supported currencies: Gold or Gems; the selected currency determines which player balance is charged or credited.
        public string currencyType;
        public int amount;
    }

    // Executes guild donate result dto operation.
    [Serializable]
    public class GuildDonateResultDto
    {
        public int goldSpent;
        public int gemSpent;
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

    // Executes guild message dto operation.
    [Serializable]
    public class GuildMessageDTO
    {
        public int messageId;
        public int senderId;
        public string senderName;
        public string content;
        public int messageType;
        public int senderRole;
        public string sentAt;
    }

    // Executes send guild message request operation.
    [Serializable]
    public class SendGuildMessageRequest
    {
        public string content;
    }

    // Executes guild log dto operation.
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

    // Executes guild rank response dto operation.
    [System.Serializable]
    public class GuildRankResponseDto
    {
        public int rank;
        public int guildId;
        public string name;
        public int iconId;
        public int level;
        public int totalMedals;
        public int totalFeats;
        public int memberCount;
        public int maxMembers;
    }
