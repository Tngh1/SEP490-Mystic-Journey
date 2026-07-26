namespace MysticJourney.API.Core
{
    // Lưu toàn bộ cấu hình kết nối backend.
    // Khi deploy lên VPS/domain thật, chỉ cần đổi BaseUrl ở đây.
    public static class ApiConfig
    {
        // URL gốc của backend. Đổi thành domain thật khi deploy.
        // Local:      "http://localhost:5176" (HTTP profile)
        // Production: "https://api.mysticjourney.com"
        public const string BaseUrl = "http://localhost:5176";

        // Thời gian tối đa chờ response (giây)
        public const int Timeout = 30;

        // Header mặc định gửi kèm mọi request
        public const string ContentType = "application/json";
        public const string Accept = "application/json";

        // ═══════════════════════════════════════════════════════════════════════
        // KEY LƯU DỮ LIỆU PHIÊN TRONG PLAYERPREFS
        // ═══════════════════════════════════════════════════════════════════════
        public const string AccessTokenKey = "mj_access_token";
        public const string PlayerProfileIdKey = "mj_player_profile_id";
        public const string AccountIdKey = "mj_account_id";
        public const string UserNameKey = "mj_user_name";
        public const string PlayerLevelKey = "mj_player_level";
        public const string PlayerClassKey = "mj_player_class";
        public const string LastMapNameKey = "mj_last_map_name";
        public const string PositionXKey = "mj_position_x";
        public const string PositionYKey = "mj_position_y";

        // Remember Me keys
        public const string RememberMeKey = "mj_remember_me";
        public const string SavedUsernameKey = "mj_saved_username";

        // ═══════════════════════════════════════════════════════════════════════
        // AUTH CONTROLLER - Xác thực và quản lý tài khoản
        // ═══════════════════════════════════════════════════════════════════════
        public const string AuthLogin = "/api/auth/login";
        public const string AuthMe = "/api/auth/me";
        public const string AuthLogout = "/api/auth/logout";

        // ═══════════════════════════════════════════════════════════════════════
        // PLAYER CONTROLLER - Trạng thái online
        // ═══════════════════════════════════════════════════════════════════════
        public const string PlayerHeartbeat = "/api/player/heartbeat";

        // ═══════════════════════════════════════════════════════════════════════
        // CHARACTERS CONTROLLER - Nhân vật của người chơi
        // ═══════════════════════════════════════════════════════════════════════
        public const string CharacterCreate = "/api/characters";
        public const string CharacterStats = "/api/characters/stats";
        public const string CharacterHp = "/api/characters/hp";
        public const string CharacterBuffs = "/api/characters/buffs";
        public const string CharacterLevelUpOptions = "/api/characters/level-up-options";
        public const string CharacterAllocateStat = "/api/characters/allocate-stat";

        // ═══════════════════════════════════════════════════════════════════════
        // INVENTORY CONTROLLER - Hành trang
        // ═══════════════════════════════════════════════════════════════════════
        public const string InventoryMe = "/api/inventory/me";
        public const string InventoryEquip = "/api/inventory/equip-item";
        public const string InventoryUnequip = "/api/inventory/unequip-item";
        public const string InventoryConsume = "/api/inventory/consume-item";

        // CURRENCY CONTROLLER - Player gold/gems balance
        public const string CurrencyBalance = "/api/currencies/me/balance";

        // ═══════════════════════════════════════════════════════════════════════
        // GUILDS CONTROLLER - Hệ thống Bang hội v3
        // ═══════════════════════════════════════════════════════════════════════
        public const string GuildMyGuild = "/api/guilds/my-guild"; // GET
        public const string GuildList = "/api/guilds"; // GET, POST
        public const string GuildDetail = "/api/guilds/{id}"; // GET, DELETE
        public const string GuildMembers = "/api/guilds/{id}/members"; // GET
        public const string GuildApply = "/api/guilds/{id}/apply"; // POST
        public const string GuildLeave = "/api/guilds/{id}/leave"; // POST
        public const string GuildLevelUp = "/api/guilds/{id}/level-up"; // POST
        public const string GuildApplications = "/api/guilds/{id}/applications"; // GET
        public const string GuildApproveApp = "/api/guilds/{id}/applications/{appId}/approve"; // POST
        public const string GuildRejectApp = "/api/guilds/{id}/applications/{appId}/reject"; // POST
        public const string GuildInvite = "/api/guilds/{id}/invite"; // POST
        public const string GuildKick = "/api/guilds/{id}/members/{memberId}/kick"; // POST
        public const string GuildPromote = "/api/guilds/{id}/members/{memberId}/promote"; // POST
        public const string GuildDemote = "/api/guilds/{id}/members/{memberId}/demote"; // POST
        public const string GuildTransferLeader = "/api/guilds/{id}/transfer-leader"; // POST
        public const string GuildNotice = "/api/guilds/{id}/notice"; // PUT
        public const string GuildIcon = "/api/guilds/{id}/icon"; // PUT
        public const string GuildDonate = "/api/guilds/{id}/donate"; // POST
        public const string GuildLogs = "/api/guilds/{id}/logs"; // GET
        public const string GuildChat = "/api/guilds/{id}/chat"; // GET, POST

        // ═══════════════════════════════════════════════════════════════════════
        // PLAYER PROFILES CONTROLLER - Hồ sơ người chơi
        // ═══════════════════════════════════════════════════════════════════════
        // Dùng cho cả GET (lấy profile) và PUT (cập nhật profile)
        public const string PlayerProfileById = "/api/playerprofiles/{0}";
        public const string PlayerProfileChangeName = "/api/playerprofiles/change-name";
        public const string PlayerProfileMeFriends = "/api/playerprofiles/me/friends";

        // Friend API
        public static string GetFriendListEndpoint => "/api/friend";
        public static string GetFriendRequestsEndpoint => "/api/friend/requests";
        public static string GetFriendBlocksEndpoint => "/api/friend/blocks";
        public static string SearchPlayersEndpoint => "/api/friend/search";
        public static string GetFriendProfileEndpoint => "/api/friend/profile/{id}";
        public static string SendFriendRequestEndpoint => "/api/friend/request";
        public static string AcceptFriendRequestEndpoint => "/api/friend/accept/{requesterId}";
        public static string DeclineFriendRequestEndpoint => "/api/friend/decline/{requesterId}";
        public static string RemoveFriendEndpoint => "/api/friend/{targetId}";
        public static string BlockPlayerEndpoint => "/api/friend/block";
        public static string UnblockPlayerEndpoint => "/api/friend/block/{targetId}";

        // ═══════════════════════════════════════════════════════════════════════
        // PLAYER SKILLS CONTROLLER - Skills của người chơi
        // ═══════════════════════════════════════════════════════════════════════
        public const string PlayerSkillsMe = "/api/player-skills/me";
        public const string PlayerSkillsUpgrade = "/api/player-skills/upgrade";
        public const string PlayerSkillsEquip = "/api/player-skills/equip";
        public const string PlayerSkillsDismantle = "/api/player-skills/dismantle";
        public const string PlayerSkillsRecordCast = "/api/player-skills/record-cast/{0}";

        // ═══════════════════════════════════════════════════════════════════════
        // PLAYER QUESTS CONTROLLER - Nhiệm vụ của người chơi
        // ═══════════════════════════════════════════════════════════════════════
        public const string PlayerQuestMe = "/api/playerquests/me";
        public const string PlayerQuestDetail = "/api/playerquests/{0}";
        public const string PlayerQuestAccept = "/api/playerquests/accept";
        public const string PlayerQuestBatchProgress = "/api/playerquests/batch-progress";
        public const string PlayerQuestComplete = "/api/playerquests/complete";
        public const string PlayerQuestClaim = "/api/playerquests/claim";

        // ═══════════════════════════════════════════════════════════════════════
        // DUNGEONS CONTROLLER - Phó bản và Session
        // ═══════════════════════════════════════════════════════════════════════
        public const string DungeonAll = "/api/dungeons";
        public const string DungeonById = "/api/dungeons/{0}";
        public const string DungeonEnter = "/api/dungeons/{0}/enter";
        public const string DungeonSessionProgress = "/api/dungeons/session/{0}/progress";
        public const string DungeonSessionComplete = "/api/dungeons/session/{0}/complete";
        public const string DungeonSessionClaimReward = "/api/dungeons/session/{0}/claim-reward";

        // ═══════════════════════════════════════════════════════════════════════
        // MONSTERS CONTROLLER - Quái vật và Spawns
        // ═══════════════════════════════════════════════════════════════════════
        public const string MonsterById = "/api/monsters/{0}";
        public const string MonsterByIdForPlayer = "/api/monsters/{0}/me";
        public const string MonsterCatalogForPlayer = "/api/monsters/me/catalog";
        public const string MonsterSpawns = "/api/monsters/spawns";
        public const string MonsterDiscover = "/api/monsters/{0}/discover";
        public const string MonsterDefeat = "/api/monsters/{0}/defeat";

        // ═══════════════════════════════════════════════════════════════════════
        // WORLD CONTROLLER - Thế giới game
        // ═══════════════════════════════════════════════════════════════════════
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
        public const string ChatFriendMessages = "/api/chat/friend/messages";
        public const string ChatFriendSend = "/api/chat/friend/send";
        public const string ChatFriendReport = "/api/chat/friend/report";

        // ═══════════════════════════════════════════════════════════════════════
        // QUESTS CONTROLLER - Catalog nhiệm vụ
        // ═══════════════════════════════════════════════════════════════════════
        public const string QuestById = "/api/quests/{0}";

        // ═══════════════════════════════════════════════════════════════════════
        // ACHIEVEMENTS CONTROLLER - Thành tựu
        // ═══════════════════════════════════════════════════════════════════════
        public const string AchievementAll = "/api/achievements";
        public const string AchievementMe = "/api/achievements/me";

        // ═══════════════════════════════════════════════════════════════════════
        // SHOP ITEMS CONTROLLER - Vật phẩm cửa hàng
        // ═══════════════════════════════════════════════════════════════════════
        public const string PlayerShopFixed = "/api/shop/fixed";
        public const string PlayerShopDailyDeals = "/api/shop/daily-deals";
        public const string PlayerShopRefreshStatus = "/api/shop/daily-deals/refresh-status";
        public const string PlayerShopRefresh = "/api/shop/daily-deals/refresh";
        public const string PlayerShopPurchase = "/api/shop/purchase";

        // ═══════════════════════════════════════════════════════════════════════
        // GACHA BANNERS CONTROLLER - Banner gacha/quay thưởng
        // ═══════════════════════════════════════════════════════════════════════
        public const string GachaById = "/api/gachabanners/{0}";
        public const string GachaPull = "/api/gachabanners/{0}/pull";
        public const string GachaHistory = "/api/gachabanners/history";

        // ═══════════════════════════════════════════════════════════════════════
        // MAILS CONTROLLER - Thư
        // ═══════════════════════════════════════════════════════════════════════
        public const string MailMe = "/api/mails/me";
        public const string MailById = "/api/mails/{0}";
        public const string MailRead = "/api/mails/{0}/read";
        public const string MailClaim = "/api/mails/{0}/claim";

        // ═══════════════════════════════════════════════════════════════════════
        // DAILY LOGIN REWARDS CONTROLLER - Thưởng đăng nhập hàng ngày
        // ═══════════════════════════════════════════════════════════════════════
        public const string DailyLoginRewardCurrentMonth = "/api/dailyloginrewards/current-month";

        // ═══════════════════════════════════════════════════════════════════════
        // SKINS CONTROLLER - Áo
        // ═══════════════════════════════════════════════════════════════════════
        public const string SkinEquip = "/api/skins/equip";
        public const string SkinUnequip = "/api/skins/unequip";

    }
}
