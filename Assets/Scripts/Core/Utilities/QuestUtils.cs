using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.Core.Utilities
{
    // Initializes a new default instance of the QuestUtils class.
    public static class QuestUtils
    {
        // Executes normalize main quests operation.
        // Validates input parameters against null or empty values.
        public static List<PlayerQuestResponse> NormalizeMainQuests(IEnumerable<PlayerQuestResponse> source)
        {
            string currentMap = WorldState.CurrentMapName ?? string.Empty;

            bool IsOnCurrentMap(PlayerQuestResponse q)
            {
                if (q == null || string.IsNullOrWhiteSpace(currentMap) || string.IsNullOrWhiteSpace(q.MapName))
                    return false;
                return IsSameMap(q.MapName, currentMap);
            }

            return (source ?? Enumerable.Empty<PlayerQuestResponse>())
                .Where(IsMainQuest)
                .OrderBy(q => IsOnCurrentMap(q) ? 0 : 1)
                .ThenBy(QuestStatusPriority)
                .ThenBy(q => q.RequiredLevel)
                .ThenBy(q => q.QuestId)
                .ToList();
        }

        // Executes pick preferred quest operation.
        // Validates input parameters against null or empty values.
        public static PlayerQuestResponse PickPreferredQuest(IEnumerable<PlayerQuestResponse> source)
        {
            var quests = source?.Where(q => q != null && !IsStatus(q, "Claimed")).ToList() ?? new List<PlayerQuestResponse>();
            if (quests.Count == 0) return null;

            string currentMap = WorldState.CurrentMapName ?? string.Empty;

            bool IsOnCurrentMap(PlayerQuestResponse q)
            {
                if (q == null || string.IsNullOrWhiteSpace(currentMap) || string.IsNullOrWhiteSpace(q.MapName))
                    return true;
                return IsSameMap(q.MapName, currentMap);
            }

            var inProgressCurrent = quests.Where(q => IsStatus(q, "InProgress") && IsOnCurrentMap(q)).OrderBy(q => q.QuestId).FirstOrDefault();
            if (inProgressCurrent != null) return inProgressCurrent;

            var completedCurrent = quests.Where(q => IsStatus(q, "Completed") && IsOnCurrentMap(q)).OrderBy(q => q.QuestId).FirstOrDefault();
            if (completedCurrent != null) return completedCurrent;

            var inProgressAny = quests.Where(q => IsStatus(q, "InProgress")).OrderBy(q => q.QuestId).FirstOrDefault();
            if (inProgressAny != null) return inProgressAny;

            var completedAny = quests.Where(q => IsStatus(q, "Completed")).OrderBy(q => q.QuestId).FirstOrDefault();
            if (completedAny != null) return completedAny;

            var notStartedCurrent = quests.Where(q => IsOnCurrentMap(q)).OrderBy(q => q.QuestId).FirstOrDefault();
            if (notStartedCurrent != null) return notStartedCurrent;

            return quests.OrderBy(q => q.QuestId).FirstOrDefault();
        }


        // Executes find same quest operation.
        // Validates input parameters against null or empty values.
        public static PlayerQuestResponse FindSameQuest(IEnumerable<PlayerQuestResponse> source, PlayerQuestResponse target)
        {
            if (target == null) return null;
            return source?.FirstOrDefault(q => q != null && q.QuestId == target.QuestId);
        }

        // Normalizes world map names and maps aliases (such as ElfForest) to canonical map identifiers.
        public static string NormalizeMapName(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? string.Empty
                : name.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        }

        // Executes is same map operation.
        // Validates input parameters against null or empty values.
        public static bool IsSameMap(string a, string b)
        {
            string na = NormalizeMapName(a), nb = NormalizeMapName(b);
            if (na.Length == 0 || nb.Length == 0) return false;
            return na.IndexOf(nb, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   nb.IndexOf(na, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Executes is quest on different map operation.
        // Validates input parameters against null or empty values.
        // Evaluates conditions and returns a boolean result.
        public static bool IsQuestOnDifferentMap(PlayerQuestResponse quest)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.MapName)) return false;
            string currentMap = WorldState.CurrentMapName ?? string.Empty;
            if (currentMap.Length == 0) return false;
            return !IsSameMap(quest.MapName, currentMap);
        }

        // Executes is main quest operation.
        // Validates input parameters against null or empty values.
        // Evaluates conditions and returns a boolean result.
        public static bool IsMainQuest(PlayerQuestResponse quest)
        {
            if (quest == null) return false;
            if (string.IsNullOrWhiteSpace(quest.QuestType)) return true;

            var normalized = quest.QuestType
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
            return string.Equals(normalized, "Main", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "MainQuest", System.StringComparison.OrdinalIgnoreCase) ||
                   normalized.IndexOf("Main", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Executes is status operation.
        public static bool IsStatus(PlayerQuestResponse quest, string status)
        {
            return quest != null && string.Equals(quest.Status, status, System.StringComparison.OrdinalIgnoreCase);
        }

        // Executes quest status priority operation.
        public static int QuestStatusPriority(PlayerQuestResponse quest)
        {
            if (IsStatus(quest, "InProgress")) return 0;
            if (IsStatus(quest, "Completed")) return 1;
            if (IsStatus(quest, "NotStarted")) return 2;
            if (IsStatus(quest, "Claimed")) return 3;
            return 4;
        }

        // Executes is auto complete quest operation.
        public static bool IsAutoCompleteQuest(string objectiveType)
        {
            var t = objectiveType ?? "";
            return string.Equals(t, "Collect",  System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(t, "Defeat",   System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(t, "Explore",  System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(t, "OpenChest",System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(t, "Talk",     System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(t, "Interact", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(t, "EquipSkill",System.StringComparison.OrdinalIgnoreCase);
        }

        // Executes status label operation.
        public static string StatusLabel(PlayerQuestResponse quest)
        {
            if (quest == null) return "Unknown";
            return quest.Status switch
            {
                "NotStarted" => "Available",
                "InProgress" => "In Progress",
                "Completed" => IsAutoCompleteQuest(quest.ObjectiveType) ? "Completed" : "<color=orange>Return to Quest Giver</color>",
                "Claimed" => "Claimed",
                _ => quest.Status ?? "Unknown"
            };
        }


        // Executes normalize identity operation.
        // Validates input parameters against null or empty values.
        public static string NormalizeIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var builder = new System.Text.StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        // Executes is world objective operation.
        // Evaluates conditions and returns a boolean result.
        public static bool IsWorldObjective(PlayerQuestResponse quest)
        {
            if (quest == null) return false;

            return string.Equals(quest.ObjectiveType, "Collect", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(quest.ObjectiveType, "Interact", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(quest.ObjectiveType, "Gather", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(quest.ObjectiveType, "Fetch", System.StringComparison.OrdinalIgnoreCase);
        }

        // Executes target matches operation.
        // Evaluates conditions and returns a boolean result.
        public static bool TargetMatches(string objectiveTarget, string objectKey, string displayName)
        {
            var target = NormalizeIdentity(objectiveTarget);
            if (target.Length < 4) return false;

            return Contains(NormalizeIdentity(objectKey), target) ||
                   Contains(NormalizeIdentity(displayName), target);

            // Executes contains operation.
            // Evaluates conditions and returns a boolean result.
            static bool Contains(string candidate, string target)
            {
                if (candidate.Length == 0) return false;
                if (string.Equals(candidate, target)) return true;
                return candidate.Length >= 4 && (candidate.Contains(target) || target.Contains(candidate));
            }
        }

        // Executes objective line operation.
        public static string ObjectiveLine(PlayerQuestResponse quest)
        {
            if (quest == null) return string.Empty;

            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            int current = Mathf.Clamp(quest.Progress, 0, Mathf.Max(1, quest.TargetAmount));
            int target = Mathf.Max(1, quest.TargetAmount);
            var objective = quest.ObjectiveType ?? "Explore";
            var targetName = quest.ObjectiveTarget ?? "target";
            var location = quest.ObjectiveLocation ?? quest.RegionName ?? quest.MapName ?? "the world";
            return $"{objective}: {targetName} at {location}  {current}/{target}";
        }

        // Executes reward line operation.
        public static string RewardLine(PlayerQuestResponse quest)
        {
            if (quest == null) return string.Empty;

            var parts = new List<string>();
            if (quest.RewardExperience > 0) parts.Add($"EXP +{quest.RewardExperience}");
            if (quest.RewardGold > 0) parts.Add($"Gold +{quest.RewardGold:0}");
            if (quest.RewardGems > 0) parts.Add($"Gems +{quest.RewardGems:0}");
            if (quest.RewardItems != null && quest.RewardItems.Count > 0)
            {
                foreach (var item in quest.RewardItems)
                {
                    var itemLabel = !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName : $"Item #{item.ItemId}";
                    var quantity = Mathf.Max(1, item.Quantity);
                    parts.Add($"Item: {itemLabel} x{quantity}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(quest.RewardItemName))
            {
                parts.Add($"Item: {quest.RewardItemName}");
            }

            if (quest.RewardSkills != null && quest.RewardSkills.Count > 0)
            {
                foreach (var skill in quest.RewardSkills)
                {
                    var skillLabel = !string.IsNullOrWhiteSpace(skill.SkillName) ? skill.SkillName : $"Skill #{skill.SkillId}";
                    if (!string.IsNullOrWhiteSpace(skill.ClassRequirement))
                        skillLabel += $" ({skill.ClassRequirement})";
                    parts.Add($"Skill: {skillLabel}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(quest.RewardSkillName) || quest.RewardSkillId.HasValue)
            {
                var skillLabel = !string.IsNullOrWhiteSpace(quest.RewardSkillName)
                    ? quest.RewardSkillName
                    : $"Skill #{quest.RewardSkillId.Value}";
                parts.Add($"Skill: {skillLabel}");
            }

            return parts.Count == 0 ? "No reward." : string.Join(" | ", parts);
        }
    }
}
