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

        // ── AccountsController ─────────────────────────────────────
        public const string LoginGame = "/api/accounts/login-game";  // POST, không cần auth
        public const string Logout    = "/api/accounts/logout";       // POST, cần auth
        public const string Me        = "/api/accounts/me";           // GET,  cần auth

        // ── PlayerProfilesController ───────────────────────────────
        public const string PlayerProfileById    = "/api/playerprofiles/{0}"; // {0} = playerProfileId
        public const string PlayerProfileUpdate  = "/api/playerprofiles/{0}"; // PUT, cần auth

        // ── InventoryController ────────────────────────────────────
        public const string InventoryMe      = "/api/inventory/me";           // GET,  cần auth
        public const string InventoryEquip   = "/api/inventory/equip-item";   // POST, cần auth
        public const string InventoryUnequip = "/api/inventory/unequip-item"; // POST, cần auth
        public const string InventoryConsume = "/api/inventory/consume-item"; // POST, cần auth

        // ── DungeonsController ─────────────────────────────────────
        public const string DungeonAll = "/api/dungeons";      // GET, không cần auth
        public const string DungeonById = "/api/dungeons/{0}"; // {0} = dungeonConfigId

        // ── QuestsController ───────────────────────────────────────
        public const string QuestAll = "/api/quests";          // GET, không cần auth
        public const string QuestById = "/api/quests/{0}";     // {0} = questId

        // ── AchievementsController ─────────────────────────────────
        public const string AchievementAll = "/api/achievements";       // GET, không cần auth
        public const string AchievementById = "/api/achievements/{0}";  // {0} = achievementId

        // ── GachaBannersController ─────────────────────────────────
        public const string GachaAll = "/api/gachabanners";      // GET, không cần auth
        public const string GachaById = "/api/gachabanners/{0}"; // {0} = gachaBannerId

        // ── MailsController ────────────────────────────────────────
        public const string MailById     = "/api/mails/{0}";            // {0} = mailId
        public const string MailByPlayer = "/api/mails/player/{0}";     // {0} = playerProfileId
        public const string MailRead     = "/api/mails/{0}/read";        // POST, cần auth
        public const string MailClaim    = "/api/mails/{0}/claim";       // POST, cần auth
        public const string MailDelete   = "/api/mails/{0}";             // DELETE, cần auth

        // ── DailyLoginRewardsController ────────────────────────────
        public const string DailyLoginRewards = "/api/dailyloginrewards"; // GET

        // ── ShopItemsController ────────────────────────────────────
        public const string ShopItems = "/api/shopitems"; // GET, không cần auth

        // ── SkinsController ────────────────────────────────────────
        public const string SkinEquip   = "/api/skins/equip";   // POST, cần auth
        public const string SkinUnequip = "/api/skins/unequip"; // POST, cần auth
    }
}
