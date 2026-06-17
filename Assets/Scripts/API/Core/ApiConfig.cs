namespace MysticJourney.API.Core
{
    public static class ApiConfig
    {
        public const string BaseUrl = "http://localhost:5176";
        public const int Timeout = 30;

        public const string ContentType = "application/json";
        public const string Accept = "application/json";

        public const string AccessTokenKey = "mj_access_token";
        public const string PlayerProfileIdKey = "mj_player_profile_id";
        public const string AccountIdKey = "mj_account_id";
        public const string UserNameKey = "mj_user_name";
        public const string LastMapNameKey = "mj_last_map_name";
        public const string PositionXKey = "mj_position_x";
        public const string PositionYKey = "mj_position_y";

        public const string LoginGame = "/api/accounts/login-game";
        public const string Logout = "/api/accounts/logout";
        public const string Me = "/api/accounts/me";

        public const string PlayerProfileById = "/api/playerprofiles/{0}";
        public const string PlayerProfileUpdate = "/api/playerprofiles/{0}";

        public const string InventoryMe = "/api/inventory/me";
        public const string InventoryEquip = "/api/inventory/equip-item";
        public const string InventoryUnequip = "/api/inventory/unequip-item";
        public const string InventoryConsume = "/api/inventory/consume-item";

        public const string DungeonAll = "/api/dungeons";
        public const string DungeonById = "/api/dungeons/{0}";

        public const string QuestAll = "/api/quests";
        public const string QuestById = "/api/quests/{0}";

        public const string AchievementAll = "/api/achievements";
        public const string AchievementById = "/api/achievements/{0}";

        public const string GachaAll = "/api/gachabanners";
        public const string GachaById = "/api/gachabanners/{0}";

        public const string MailById = "/api/mails/{0}";
        public const string MailByPlayer = "/api/mails/player/{0}";
        public const string MailRead = "/api/mails/{0}/read";
        public const string MailClaim = "/api/mails/{0}/claim";
        public const string MailDelete = "/api/mails/{0}";

        public const string DailyLoginRewards = "/api/dailyloginrewards";
        public const string DailyLoginStatus = "/api/dailyloginrewards/status";
        public const string DailyLoginClaim = "/api/dailyloginrewards/claim";

        public const string ShopItems = "/api/shopitems";

        public const string SkinEquip = "/api/skins/equip";
        public const string SkinUnequip = "/api/skins/unequip";

        public const string PlayerQuestMe = "/api/playerquests/me";
        public const string PlayerQuestDetail = "/api/playerquests/{0}";
        public const string PlayerQuestAccept = "/api/playerquests/accept";
        public const string PlayerQuestBatch = "/api/playerquests/batch-progress";
        public const string PlayerQuestComplete = "/api/playerquests/complete";
        public const string PlayerQuestClaim = "/api/playerquests/claim";

        public const string WorldState = "/api/world/state";
        public const string WorldPosition = "/api/world/position";
        public const string WorldNpcTalk = "/api/world/npc/talk";
        public const string WorldChestOpen = "/api/world/chests/open";
        public const string WorldInteractObject = "/api/world/interactions";
        public const string WorldDailyLoginClaim = "/api/world/daily-login/claim";
    }
}
