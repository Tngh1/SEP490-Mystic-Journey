using UnityEngine;

namespace MysticJourney.Core.Utilities
{
    public static class GameConstants
    {
        public static class Scenes
        {
            public const string Bootstrap = "Bootstrap";
            public const string Intro = "Intro";
            // Tên scene THẬT trong Build Settings là "MainMenuScene" (không phải "MainMenu").
            // Hằng cũ ghi "MainMenu" nên bất kỳ LoadScene dùng nó đều fail im lặng.
            public const string MainMenu = "MainMenuScene";
            public const string Loading = "Loading";
            public const string CharacterCreation = "CharacterCreation";
            public const string Main = "Main";
            public const string ElfForest = "ElfForest";
            public const string FrozenMountain = "FrozenMountain";
            public const string Castle = "Castle";
            public const string AbandonedCastle = "Abandoned  Castle";
        }

        public static class UIElements
        {
            public const string QuestPanel = "QuestPanel";
            public const string QuestTracker = "QuestTracker";
            public const string QuestPopup = "QuestPopup";
            public const string PopupLayer = "PopupLayer";
            public const string NPCPanel = "NPCPanel";

            public const string AllButton = "AllButton";
            public const string InProgressButton = "InProgressButton";
            public const string CompletedButton = "CompletedButton";
            public const string AllRegionsButton = "AllRegionsButton";
            public const string RefreshButton = "RefreshButton";
            public const string CloseButton = "CloseButton";

            public const string AcceptQuestButton = "AcceptQuestButton";
            public const string CompleteQuestButton = "CompleteQuestButton";
            public const string DeclineQuestButton = "DeclineQuestButton";
            public const string ClaimQuestButton = "ClaimQuestButton";
            public const string ClaimedButton = "ClaimedButton";
            public const string QuestActionButton = "QuestActionButton";
            public const string PrimaryActionButton = "PrimaryActionButton";
            public const string AcceptButton = "AcceptButton";

            public const string SettingsTabAudio = "SettingsTabAudio";
            public const string SettingsTabVideo = "SettingsTabVideo";
            public const string SettingsTabControls = "SettingsTabControls";

            public const string NpcElderRowan = "ElderRowan";
            public const string NpcMageOld = "MageOld";
            public const string NpcPrefix = "NPC";
            public const string ElderRowanInteractable = "ElderRowanInteractable";
            public const string QuestItemTag = "QuestItem";
        }

        public static class WorldDefaults
        {
            public const string DefaultMap = "ElfForest";
            public const string DefaultPlayerClass = "Knight";
            public const string FallbackQuestGiver = "Elder Rowan";
            public static readonly Vector3 DefaultSpawnPosition = new(11.9f, 17.8f, 0f);
        }

        public static class UnlockLevels
        {
            public const int Inventory = 2;
            public const int MiniMap = 2;
            public const int Shop = 3;
            public const int Gacha = 4;
            public const int Skill = 5;
            public const int Guild = 5;
        }

        public static class Timing
        {
            public const float PositionSyncInterval = 1f;
            public const float PositionSyncDistanceThreshold = 0.5f;
            public const float SceneScanInterval = 0.35f;
            public const float InteractionCooldown = 0.25f;
            public const float PopupDisplayDuration = 2.4f;
            public const float BatchSyncInterval = 1f;
            public const float StaleBatchThreshold = 5f;
            public const float MuteThreshold = 0.001f;
        }

        public static class PlayerClasses
        {
            public const string Knight = "Knight";
            public const string Mage = "Mage";
            public const string Archer = "Archer";
            public static readonly string[] All = { Knight, Mage, Archer };
        }
    }
}
