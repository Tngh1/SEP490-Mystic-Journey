using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.Core.Utilities
{
    public static class QuestUtils
    {
        public static List<PlayerQuestResponse> NormalizeMainQuests(IEnumerable<PlayerQuestResponse> source)
        {
            string currentMap = WorldState.CurrentMapName ?? string.Empty;

            bool IsOnCurrentMap(PlayerQuestResponse q)
            {
                if (q == null || string.IsNullOrWhiteSpace(currentMap) || string.IsNullOrWhiteSpace(q.MapName))
                    return false;
                return q.MapName.IndexOf(currentMap, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       currentMap.IndexOf(q.MapName, System.StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return (source ?? Enumerable.Empty<PlayerQuestResponse>())
                .Where(IsMainQuest)
                .OrderBy(q => IsOnCurrentMap(q) ? 0 : 1)
                .ThenBy(QuestStatusPriority)
                .ThenBy(q => q.RequiredLevel)
                .ThenBy(q => q.QuestId)
                .ToList();
        }

        public static PlayerQuestResponse PickPreferredQuest(IEnumerable<PlayerQuestResponse> source)
        {
            var quests = source?.Where(q => q != null && !IsStatus(q, "Claimed")).ToList() ?? new List<PlayerQuestResponse>();
            if (quests.Count == 0) return null;

            string currentMap = WorldState.CurrentMapName ?? string.Empty;

            bool IsOnCurrentMap(PlayerQuestResponse q)
            {
                if (q == null || string.IsNullOrWhiteSpace(currentMap) || string.IsNullOrWhiteSpace(q.MapName))
                    return true;
                return q.MapName.IndexOf(currentMap, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       currentMap.IndexOf(q.MapName, System.StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Ưu tiên theo thứ tự tiến trình nghiêm ngặt (ưu tiên QuestId nhỏ nhất của Main Quest):
            // 1. Quần thể InProgress trên map hiện tại (QuestId nhỏ nhất)
            var inProgressCurrent = quests.Where(q => IsStatus(q, "InProgress") && IsOnCurrentMap(q)).OrderBy(q => q.QuestId).FirstOrDefault();
            if (inProgressCurrent != null) return inProgressCurrent;

            // 2. Completed (chờ trả) trên map hiện tại (QuestId nhỏ nhất)
            var completedCurrent = quests.Where(q => IsStatus(q, "Completed") && IsOnCurrentMap(q)).OrderBy(q => q.QuestId).FirstOrDefault();
            if (completedCurrent != null) return completedCurrent;

            // 3. InProgress trên bất kỳ map nào (QuestId nhỏ nhất)
            var inProgressAny = quests.Where(q => IsStatus(q, "InProgress")).OrderBy(q => q.QuestId).FirstOrDefault();
            if (inProgressAny != null) return inProgressAny;

            // 4. Completed trên bất kỳ map nào (QuestId nhỏ nhất)
            var completedAny = quests.Where(q => IsStatus(q, "Completed")).OrderBy(q => q.QuestId).FirstOrDefault();
            if (completedAny != null) return completedAny;

            // 5. NotStarted (chưa nhận) - luôn chọn QuestId nhỏ nhất của map hiện tại
            var notStartedCurrent = quests.Where(q => IsOnCurrentMap(q)).OrderBy(q => q.QuestId).FirstOrDefault();
            if (notStartedCurrent != null) return notStartedCurrent;

            return quests.OrderBy(q => q.QuestId).FirstOrDefault();
        }


        public static PlayerQuestResponse FindSameQuest(IEnumerable<PlayerQuestResponse> source, PlayerQuestResponse target)
        {
            if (target == null) return null;
            return source?.FirstOrDefault(q => q != null && q.QuestId == target.QuestId);
        }

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

        public static bool IsStatus(PlayerQuestResponse quest, string status)
        {
            return quest != null && string.Equals(quest.Status, status, System.StringComparison.OrdinalIgnoreCase);
        }

        public static int QuestStatusPriority(PlayerQuestResponse quest)
        {
            if (IsStatus(quest, "InProgress")) return 0;
            if (IsStatus(quest, "Completed")) return 1;
            if (IsStatus(quest, "NotStarted")) return 2;
            if (IsStatus(quest, "Claimed")) return 3;
            return 4;
        }

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

        public static string ObjectiveLine(PlayerQuestResponse quest)
        {
            if (quest == null) return string.Empty;

            int current = Mathf.Clamp(quest.Progress, 0, Mathf.Max(1, quest.TargetAmount));
            int target = Mathf.Max(1, quest.TargetAmount);
            var objective = quest.ObjectiveType ?? "Explore";
            var targetName = quest.ObjectiveTarget ?? "target";
            var location = quest.ObjectiveLocation ?? quest.RegionName ?? quest.MapName ?? "the world";
            return $"{objective}: {targetName} at {location}  {current}/{target}";
        }

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
