using System;

namespace MysticJourney.API.Core
{
    // Initializes a new default instance of the ApiConfig class.
    public static class ApiConfig
    {
        public const string DefaultBaseUrl = "https://api.mystic-journey.io.vn";

        // Executes base url operation.
        // Validates input parameters against null or empty values.
        public static string BaseUrl
        {
            get
            {
                var configured = Environment.GetEnvironmentVariable("MJ_API_BASE_URL");
                return string.IsNullOrWhiteSpace(configured) ? DefaultBaseUrl : configured.TrimEnd('/');
            }
        }

        public const int Timeout = 30;

        public const string ContentType = "application/json";
        public const string Accept = "application/json";

        public const string AccessTokenKey = "mj_access_token";
        public const string RefreshTokenKey = "mj_refresh_token";
        public const string PlayerProfileIdKey = "mj_player_profile_id";
        public const string AccountIdKey = "mj_account_id";
        public const string UserNameKey = "mj_user_name";
        public const string PlayerLevelKey = "mj_player_level";
        public const string PlayerClassKey = "mj_player_class";
        public const string LastMapNameKey = "mj_last_map_name";
        public const string PositionXKey = "mj_position_x";
        public const string PositionYKey = "mj_position_y";

        public const string RememberMeKey = "mj_remember_me";
        public const string SavedUsernameKey = "mj_saved_username";

        public const string AuthLogin = "/api/auth/login";
        public const string AuthMe = "/api/auth/me";
        public const string AuthLogout = "/api/auth/logout";
        public const string AuthRefreshToken = "/api/auth/refresh-token";

        public const string GameHub = "/hubs/game";

        public const string PlayerHeartbeat = "/api/player/heartbeat";

        public const string CharacterCreate = "/api/characters";
        public const string CharacterStats = "/api/characters/stats";
        public const string CharacterHp = "/api/characters/hp";
        public const string CharacterBuffs = "/api/characters/buffs";
        public const string CharacterLevelUpOptions = "/api/characters/level-up-options";
        public const string CharacterAllocateStat = "/api/characters/allocate-stat";

        public const string WikiClasses = "/api/wiki/classes";

        public const string InventoryMe = "/api/inventory/me";
        public const string InventoryEquip = "/api/inventory/equip-item";
        public const string InventoryUnequip = "/api/inventory/unequip-item";
        public const string InventoryConsume = "/api/inventory/consume-item";

        public const string CurrencyBalance = "/api/currencies/me/balance";

        public const string GuildMyGuild = "/api/guilds/my-guild";
        public const string GuildList = "/api/guilds";
        public const string GuildDetail = "/api/guilds/{id}";
        public const string GuildMembers = "/api/guilds/{id}/members";
        public const string GuildApply = "/api/guilds/{id}/apply";
        public const string GuildLeave = "/api/guilds/{id}/leave";
        public const string GuildLevelUp = "/api/guilds/{id}/level-up";
        public const string GuildApplications = "/api/guilds/{id}/applications";
        public const string GuildApproveApp = "/api/guilds/{id}/applications/{appId}/approve";
        public const string GuildRejectApp = "/api/guilds/{id}/applications/{appId}/reject";
        public const string GuildInvite = "/api/guilds/{id}/invite";
        public const string GuildKick = "/api/guilds/{id}/members/{memberId}/kick";
        public const string GuildPromote = "/api/guilds/{id}/members/{memberId}/promote";
        public const string GuildDemote = "/api/guilds/{id}/members/{memberId}/demote";
        public const string GuildTransferLeader = "/api/guilds/{id}/transfer-leader";
        public const string GuildNotice = "/api/guilds/{id}/notice";
        public const string GuildIcon = "/api/guilds/{id}/icon";
        public const string GuildDonate = "/api/guilds/{id}/donate";
        public const string GuildLogs = "/api/guilds/{id}/logs";
        public const string GuildChat = "/api/guilds/{id}/chat";

        public const string PlayerProfileById = "/api/playerprofiles/{0}";
        public const string PlayerProfileChangeName = "/api/playerprofiles/change-name";

        // Executes get friend list endpoint operation.
        public static string GetFriendListEndpoint => "/api/friend";
        // Executes get friend requests endpoint operation.
        public static string GetFriendRequestsEndpoint => "/api/friend/requests";
        // Executes get friend blocks endpoint operation.
        public static string GetFriendBlocksEndpoint => "/api/friend/blocks";
        // Executes search players endpoint operation.
        public static string SearchPlayersEndpoint => "/api/friend/search";
        // Executes get friend profile endpoint operation.
        public static string GetFriendProfileEndpoint => "/api/friend/profile/{id}";
        // Executes send friend request endpoint operation.
        public static string SendFriendRequestEndpoint => "/api/friend/request";
        // Executes accept friend request endpoint operation.
        public static string AcceptFriendRequestEndpoint => "/api/friend/accept/{requesterId}";
        // Executes decline friend request endpoint operation.
        public static string DeclineFriendRequestEndpoint => "/api/friend/decline/{requesterId}";
        // Executes remove friend endpoint operation.
        public static string RemoveFriendEndpoint => "/api/friend/{targetId}";
        // Executes block player endpoint operation.
        public static string BlockPlayerEndpoint => "/api/friend/block";
        // Executes unblock player endpoint operation.
        public static string UnblockPlayerEndpoint => "/api/friend/block/{targetId}";

        public const string PlayerSkillsMe = "/api/player-skills/me";
        public const string PlayerSkillsUpgrade = "/api/player-skills/upgrade";
        public const string PlayerSkillsEquip = "/api/player-skills/equip";
        public const string PlayerSkillsRecordCast = "/api/player-skills/record-cast/{0}";

        public const string PlayerQuestMe = "/api/playerquests/me";
        public const string PlayerQuestDetail = "/api/playerquests/{0}";
        public const string PlayerQuestAccept = "/api/playerquests/accept";
        public const string PlayerQuestBatchProgress = "/api/playerquests/batch-progress";
        public const string PlayerQuestComplete = "/api/playerquests/complete";
        public const string PlayerQuestClaim = "/api/playerquests/claim";

        public const string DungeonAll = "/api/dungeons";
        public const string DungeonById = "/api/dungeons/{0}";
        public const string DungeonEnter = "/api/dungeons/{0}/enter";
        public const string DungeonSessionProgress = "/api/dungeons/session/{0}/progress";
        public const string DungeonSessionComplete = "/api/dungeons/session/{0}/complete";
        public const string DungeonSessionClaimReward = "/api/dungeons/session/{0}/claim-reward";

        public const string MonsterById = "/api/monsters/{0}";
        public const string MonsterByIdForPlayer = "/api/monsters/{0}/me";
        public const string MonsterCatalogForPlayer = "/api/monsters/me/catalog";
        public const string MonsterSpawns = "/api/monsters/spawns";
        public const string MonsterDiscover = "/api/monsters/{0}/discover";
        public const string MonsterDefeat = "/api/monsters/{0}/defeat";

        public const string WorldState = "/api/world/state";
        public const string WorldPosition = "/api/world/position";
        public const string WorldNpcTalk = "/api/world/npc/talk";
        public const string WorldNpcTurnIn = "/api/world/npc/turn-in";
        public const string WorldInteract = "/api/world/interactions";
        public const string WorldDailyLoginClaim = "/api/world/daily-login/claim";
        public const string WorldDailyLoginRetroClaim = "/api/world/daily-login/retro-claim";

        public const string ChatWorldMessages = "/api/chat/world/messages";
        public const string ChatWorldSend = "/api/chat/world/send";
        public const string ChatWorldReport = "/api/chat/world/report";
        public const string ChatPartyReport = "/api/chat/party/report";
        public const string ChatFriendMessages = "/api/chat/friend/messages";
        public const string ChatFriendSend = "/api/chat/friend/send";
        public const string ChatFriendReport = "/api/chat/friend/report";

        public const string QuestById = "/api/quests/{0}";

        public const string AchievementAll = "/api/achievements";
        public const string AchievementMe = "/api/achievements/me";
        public const string AchievementUnlock = "/api/achievements/me/{0}/unlock";

        public const string PlayerShopFixed = "/api/shop/fixed";
        public const string PlayerShopDailyDeals = "/api/shop/daily-deals";
        public const string PlayerShopRefreshStatus = "/api/shop/daily-deals/refresh-status";
        public const string PlayerShopRefresh = "/api/shop/daily-deals/refresh";
        public const string PlayerShopPurchase = "/api/shop/purchase";
        public const string PlayerShopSkins = "/api/shop/skins";
        public const string PlayerShopSkinPurchase = "/api/shop/skins/purchase";

        public const string GachaById = "/api/gachabanners/{0}";
        public const string GachaPull = "/api/gachabanners/{0}/pull";
        public const string GachaHistory = "/api/gachabanners/history";

        public const string MailMe = "/api/mailboxes/me";
        public const string MailById = "/api/mailboxes/{0}";
        public const string MailRead = "/api/mailboxes/{0}/read";
        public const string MailClaim = "/api/mailboxes/{0}/claim";

        public const string DailyLoginRewardCurrentMonth = "/api/dailyloginrewards/current-month";

        public const string SkinEquip = "/api/skins/equip";
        public const string SkinUnequip = "/api/skins/unequip";

    }
}
