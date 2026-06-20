namespace MysticJourney.API.Core
{
    // Lưu toàn bộ cấu hình kết nối backend.
    // Khi deploy lên VPS/domain thật, chỉ cần đổi BaseUrl ở đây.
    public static class ApiConfig
    {
        // URL gốc của backend. Đổi thành domain thật khi deploy.
        // Local:      "http://localhost:5176"
        // Production: "https://api.mysticjourney.com"
        public const string BaseUrl = "http://localhost:5176";

        // Thời gian tối đa chờ response (giây)
        public const int Timeout = 30;

        // Header mặc định gửi kèm mọi request
        public const string ContentType = "application/json";
        public const string Accept = "application/json";

        // Key lưu dữ liệu phiên trong PlayerPrefs
        public const string AccessTokenKey = "mj_access_token";
        public const string PlayerProfileIdKey = "mj_player_profile_id";
        public const string AccountIdKey = "mj_account_id";
        public const string UserNameKey = "mj_user_name";
        public const string PlayerLevelKey = "mj_player_level";
        public const string PlayerClassKey = "mj_player_class";
        public const string LastMapNameKey = "mj_last_map_name";
        public const string PositionXKey = "mj_position_x";
        public const string PositionYKey = "mj_position_y";

        // Auth / Account
        public const string LoginGame = "/api/accounts/login-game";
        public const string Logout = "/api/accounts/logout";
        public const string Me = "/api/accounts/me";

        // Player Profile
        public const string PlayerProfileById = "/api/playerprofiles/{0}";
        public const string PlayerProfileUpdate = "/api/playerprofiles/{0}";

        // Inventory
        public const string InventoryMe = "/api/inventory/me";
        public const string InventoryEquip = "/api/inventory/equip-item";
        public const string InventoryUnequip = "/api/inventory/unequip-item";
        public const string InventoryConsume = "/api/inventory/consume-item";

        // Dungeons
        public const string DungeonAll = "/api/dungeons";
        public const string DungeonById = "/api/dungeons/{0}";

        // Quest catalog
        public const string QuestAll = "/api/quests";
        public const string QuestById = "/api/quests/{0}";

        // Achievements
        public const string AchievementAll = "/api/achievements";
        public const string AchievementById = "/api/achievements/{0}";

        // Gacha
        public const string GachaAll = "/api/gachabanners";
        public const string GachaById = "/api/gachabanners/{0}";

        // Mail
        public const string MailById = "/api/mails/{0}";
        public const string MailByPlayer = "/api/mails/player/{0}";
        public const string MailRead = "/api/mails/{0}/read";
        public const string MailClaim = "/api/mails/{0}/claim";
        public const string MailDelete = "/api/mails/{0}";

        // Daily Login
        public const string DailyLoginRewards = "/api/dailyloginrewards";
        public const string DailyLoginStatus = "/api/dailyloginrewards/status";
        public const string DailyLoginClaim = "/api/dailyloginrewards/claim";

        // Shop
        public const string ShopItems = "/api/shopitems";

        // Skins
        public const string SkinEquip = "/api/skins/equip";
        public const string SkinUnequip = "/api/skins/unequip";

        // Player Quest runtime (UC 25)
        public const string PlayerQuestMe = "/api/playerquests/me";
        public const string PlayerQuestDetail = "/api/playerquests/{0}";
        public const string PlayerQuestAccept = "/api/playerquests/accept";
        public const string PlayerQuestBatch = "/api/playerquests/batch-progress";
        public const string PlayerQuestComplete = "/api/playerquests/complete";
        public const string PlayerQuestClaim = "/api/playerquests/claim";

        // World runtime (UC 21)
        public const string WorldState = "/api/world/state";
        public const string WorldPosition = "/api/world/position";
        public const string WorldNpcTalk = "/api/world/npc/talk";
        public const string WorldNpcTurnIn = "/api/world/npc/turn-in";
        public const string WorldChestOpen = "/api/world/chests/open";
        public const string WorldInteractObject = "/api/world/interactions";
        public const string WorldDailyLoginClaim = "/api/world/daily-login/claim";
    }
}
