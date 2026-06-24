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
        public const string LoginGame = "/api/auth/login";
        public const string Logout = "/api/auth/logout";
        public const string Me = "/api/auth/me";

        // Player Profile
        public const string PlayerProfileById = "/api/playerprofiles/{0}";
        public const string PlayerProfileUpdate = "/api/playerprofiles/{0}";

        // Inventory
        public const string InventoryMe = "/api/inventory/me";
        public const string InventoryEquip = "/api/inventory/equip-item";
        public const string InventoryUnequip = "/api/inventory/unequip-item";
        public const string InventoryConsume = "/api/inventory/consume-item";

        // Dungeons (catalog – no auth)
        public const string DungeonAll = "/api/dungeons";
        public const string DungeonById = "/api/dungeons/{0}";

        // Dungeon Session (in-game – requires auth)
        public const string DungeonEnter       = "/api/dungeons/{0}/enter";
        public const string DungeonProgress    = "/api/dungeons/session/{0}/progress";
        public const string DungeonComplete    = "/api/dungeons/session/{0}/complete";
        public const string DungeonClaimReward = "/api/dungeons/session/{0}/claim-reward";

        // Character (requires auth)
        public const string CharacterCreate  = "/api/characters";
        public const string CharacterStats   = "/api/characters/stats";
        public const string CharacterUpgrade = "/api/characters/upgrade";

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
        public const string DailyLoginRewardsCurrentMonth = "/api/dailyloginrewards/current-month";
        // BE does not expose /status; client uses /api/world/daily-login/claim to claim
        public const string DailyLoginStatus = "/api/world/daily-login/claim";
        public const string DailyLoginClaim = "/api/world/daily-login/claim";

        // Shop
        public const string ShopItems = "/api/shopitems";

        // Skills
        public const string SkillAll = "/api/skills";
        public const string SkillById = "/api/skills/{0}";
        public const string PlayerMeSkills = "/api/playerprofiles/me/skills";
        // BE does NOT expose /api/player-skills/* — upgrade/equip disabled until SkillsController is added
        public const string PlayerSkillUpgrade = "/api/player-skills/upgrade";
        public const string PlayerSkillEquip = "/api/player-skills/equip";
        public const string PlayerSkillDismantle = "/api/player-skills/dismantle";

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

        // Monsters
        public const string MonsterAll = "/api/monsters";
        public const string MonsterById = "/api/monsters/{0}";
        public const string MonsterByIdForPlayer = "/api/monsters/{0}/me";
        public const string MonsterCatalogForPlayer = "/api/monsters/me/catalog";
        public const string MonsterSpawns = "/api/monsters/spawns";
        public const string MonsterDiscover = "/api/monsters/{0}/discover";
        public const string MonsterDefeat = "/api/monsters/{0}/defeat";
    }
}
