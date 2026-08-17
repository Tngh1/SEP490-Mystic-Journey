using System;

namespace MysticJourney.Core.Utilities
{
    // Initializes a new default instance of the MapProgressionRules class.
    public static class MapProgressionRules
    {
        public const int FirstMapId = 1;

        // Executes get map id operation.
        public static int GetMapId(string mapName)
        {
            string normalized = QuestUtils.NormalizeMapName(mapName);

            if (EqualsMap(normalized, "ElfForest") || EqualsMap(normalized, "ElfLand") ||
                EqualsMap(normalized, "Map1") || EqualsMap(normalized, "Chapter1"))
                return 1;
            if (EqualsMap(normalized, "AutumnPumpkin")) return 2;
            if (EqualsMap(normalized, "FrozenMountain") || EqualsMap(normalized, "FrozenMountains"))
                return 3;
            if (EqualsMap(normalized, "AbandonedCastle") || EqualsMap(normalized, "VestigeOfAnEra"))
                return 4;

            return 0;
        }

        // Executes get map unlocked by quest operation.
        public static int GetMapUnlockedByQuest(int claimedQuestId)
        {
            return claimedQuestId switch
            {
                8 => 2,
                20 => 3,
                27 => 4,
                _ => 0,
            };
        }

        // Executes can invite to map operation.
        public static bool CanInviteToMap(int requiredMapId, int highestUnlockedMapId)
        {
            return requiredMapId <= FirstMapId || highestUnlockedMapId >= requiredMapId;
        }

        // Executes get display name operation.
        public static string GetDisplayName(int mapId)
        {
            return mapId switch
            {
                1 => "Elf Forest",
                2 => "Autumn Pumpkin",
                3 => "Frozen Mountain",
                4 => "Vestige of an Era",
                _ => "this map",
            };
        }

        // Executes equals map operation.
        private static bool EqualsMap(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
