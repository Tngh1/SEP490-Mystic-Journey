namespace MysticJourney.API.Core
{
    // Lưu toàn bộ cấu hình kết nối backend.
    // Khi deploy lên VPS/domain thật, chỉ cần đổi BaseUrl ở đây.
    public static class ApiConfig
    {
        // URL gốc của backend. Đổi thành domain thật khi deploy.
        // Local:      "https://localhost:7116" (HTTPS profile)
        // Production: "https://api.mysticjourney.com"
        public const string BaseUrl = "https://localhost:7116";

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

        // ═══════════════════════════════════════════════════════════════════════
        // AUTH CONTROLLER - Xác thực và quản lý tài khoản
        // ═══════════════════════════════════════════════════════════════════════
        public const string AuthLogin = "/api/auth/login";
        public const string AuthMe = "/api/auth/me";
        public const string AuthLogout = "/api/auth/logout";
        public const string AuthRefreshToken = "/api/auth/refresh-token";

        // ═══════════════════════════════════════════════════════════════════════
        // CHARACTERS CONTROLLER - Nhân vật của người chơi
        // ═══════════════════════════════════════════════════════════════════════
        public const string CharacterCreate = "/api/characters";
        public const string CharacterStats = "/api/characters/stats";
        public const string CharacterHp = "/api/characters/hp";
        public const string CharacterUpgrade = "/api/characters/upgrade";

        // ═══════════════════════════════════════════════════════════════════════
        // INVENTORY CONTROLLER - Hành trang
        // ═══════════════════════════════════════════════════════════════════════
        public const string InventoryMe = "/api/inventory/me";
        public const string InventoryMeFull = "/api/inventory/me/full";
        public const string InventoryEquip = "/api/inventory/equip-item";
        public const string InventoryUnequip = "/api/inventory/unequip-item";
        public const string InventoryConsume = "/api/inventory/consume-item";

        // ═══════════════════════════════════════════════════════════════════════
        // PLAYER PROFILES CONTROLLER - Hồ sơ người chơi
        // ═══════════════════════════════════════════════════════════════════════
        public const string PlayerProfileById = "/api/playerprofiles/{0}";
        public const string PlayerProfileUpdate = "/api/playerprofiles/{0}";
        public const string PlayerProfileMe = "/api/playerprofiles/me";
        public const string PlayerProfileMeFriends = "/api/playerprofiles/me/friends";

        // ═══════════════════════════════════════════════════════════════════════
        // PLAYER SKILLS CONTROLLER - Skills của người chơi
        // ═══════════════════════════════════════════════════════════════════════
        public const string PlayerSkillsMe = "/api/player-skills/me";
        public const string PlayerSkillsUpgrade = "/api/player-skills/upgrade";
        public const string PlayerSkillsEquip = "/api/player-skills/equip";
        public const string PlayerSkillsUnlock = "/api/player-skills/unlock";
        public const string PlayerSkillsDismantle = "/api/player-skills/dismantle";

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
        public const string DungeonSessionAbandon = "/api/dungeons/session/{0}/abandon";
        public const string DungeonSessionActive = "/api/dungeons/session/active";
        public const string DungeonHistory = "/api/dungeons/history";

        // ═══════════════════════════════════════════════════════════════════════
        // MONSTERS CONTROLLER - Quái vật và Spawns
        // ═══════════════════════════════════════════════════════════════════════
        public const string MonsterAll = "/api/monsters";
        public const string MonsterById = "/api/monsters/{0}";
        public const string MonsterByIdForPlayer = "/api/monsters/{0}/me";
        public const string MonsterCatalogForPlayer = "/api/monsters/me/catalog";
        public const string MonsterSpawns = "/api/monsters/spawns";
        public const string MonsterDrops = "/api/monsters/drops";
        public const string MonsterDiscover = "/api/monsters/{0}/discover";
        public const string MonsterDefeat = "/api/monsters/{0}/defeat";
        public const string MonsterSpawnsById = "/api/monsters/{0}/spawns";
        public const string MonsterCreateSpawn = "/api/monsters/spawns";

        // ═══════════════════════════════════════════════════════════════════════
        // WORLD CONTROLLER - Thế giới game
        // ═══════════════════════════════════════════════════════════════════════
        public const string WorldState = "/api/world/state";
        public const string WorldPosition = "/api/world/position";
        public const string WorldNpcTalk = "/api/world/npc/talk";
        public const string WorldNpcTurnIn = "/api/world/npc/turn-in";
        public const string WorldChestOpen = "/api/world/chests/open";
        public const string WorldInteract = "/api/world/interactions";
        public const string WorldDailyLoginClaim = "/api/world/daily-login/claim";
        public const string WorldDailyLoginRetroClaim = "/api/world/daily-login/retro-claim";

        // ═══════════════════════════════════════════════════════════════════════
        // QUESTS CONTROLLER - Catalog nhiệm vụ
        // ═══════════════════════════════════════════════════════════════════════
        public const string QuestAll = "/api/quests";
        public const string QuestById = "/api/quests/{0}";

        // ═══════════════════════════════════════════════════════════════════════
        // ITEMS CONTROLLER - Catalog vật phẩm
        // ═══════════════════════════════════════════════════════════════════════
        public const string ItemAll = "/api/items";
        public const string ItemById = "/api/items/{0}";

        // ═══════════════════════════════════════════════════════════════════════
        // SKILLS CONTROLLER - Catalog kỹ năng
        // ═══════════════════════════════════════════════════════════════════════
        public const string SkillAll = "/api/skills";
        public const string SkillById = "/api/skills/{0}";

        // ═══════════════════════════════════════════════════════════════════════
        // ACHIEVEMENTS CONTROLLER - Thành tựu
        // ═══════════════════════════════════════════════════════════════════════
        public const string AchievementAll = "/api/achievements";
        public const string AchievementById = "/api/achievements/{0}";
        public const string AchievementMe = "/api/achievements/me";

        // ═══════════════════════════════════════════════════════════════════════
        // SHOP ITEMS CONTROLLER - Vật phẩm cửa hàng
        // ═══════════════════════════════════════════════════════════════════════
        public const string ShopItemAll = "/api/shopitems";
        public const string ShopItemById = "/api/shopitems/{0}";

        // ═══════════════════════════════════════════════════════════════════════
        // GACHA BANNERS CONTROLLER - Banner gacha/quay thưởng
        // ═══════════════════════════════════════════════════════════════════════
        public const string GachaAll = "/api/gachabanners";
        public const string GachaById = "/api/gachabanners/{0}";
        public const string GachaItemsPaged = "/api/gachabanners/items-paged";
        public const string GachaAddItem = "/api/gachabanners/{0}/items";
        public const string GachaPull = "/api/gachabanners/{0}/pull";
        public const string GachaHistory = "/api/gachabanners/history";

        // ═══════════════════════════════════════════════════════════════════════
        // MAILS CONTROLLER - Thư
        // ═══════════════════════════════════════════════════════════════════════
        public const string MailMe = "/api/mails/me";
        public const string MailById = "/api/mails/{0}";
        public const string MailRead = "/api/mails/{0}/read";
        public const string MailClaim = "/api/mails/{0}/claim";
        public const string MailByIds = "/api/mails/by-ids";
        public const string MailBroadcast = "/api/mails/broadcast";

        // ═══════════════════════════════════════════════════════════════════════
        // DAILY LOGIN REWARDS CONTROLLER - Thưởng đăng nhập hàng ngày
        // ═══════════════════════════════════════════════════════════════════════
        public const string DailyLoginRewardAll = "/api/dailyloginrewards";
        public const string DailyLoginRewardCurrentMonth = "/api/dailyloginrewards/current-month";

        // ═══════════════════════════════════════════════════════════════════════
        // SKINS CONTROLLER - Áo
        // ═══════════════════════════════════════════════════════════════════════
        public const string SkinEquip = "/api/skins/equip";
        public const string SkinUnequip = "/api/skins/unequip";

        // ═══════════════════════════════════════════════════════════════════════
        // GAME SETTINGS CONTROLLER - Cài đặt game
        // ═══════════════════════════════════════════════════════════════════════
        public const string GameSettingAll = "/api/gamesettings";
        public const string GameSettingById = "/api/gamesettings/{0}";
        public const string GameSettingByKey = "/api/gamesettings/key/{0}";

    }
}
