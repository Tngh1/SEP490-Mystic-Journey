using System;
using System.Reflection;
using NUnit.Framework;

namespace MysticJourney.Tests.EditMode
{
    /// <summary>
    /// Unity Test Framework coverage for client-side quest/map rules. Reflection is intentional:
    /// production scripts currently live in predefined Assembly-CSharp, which asmdef test
    /// assemblies cannot reference directly. These tests still invoke the real compiled methods.
    /// </summary>
    public sealed class QuestBusinessRuleTests
    {
        private static Type QuestUtils => RequireType("MysticJourney.Core.Utilities.QuestUtils");

        [TestCase("Frozen Mountain", "FrozenMountain")]
        [TestCase("abandoned_castle", "abandonedcastle")]
        [TestCase("Elf-Forest", "ElfForest")]
        public void BR076_MapNamesAreNormalizedAcrossApiAndSceneFormats(string input, string expected) =>
            Assert.That(Invoke<string>("NormalizeMapName", input), Is.EqualTo(expected).IgnoreCase);

        [Test]
        public void BR076_EquivalentMapNamesCompareEqual() =>
            Assert.That(Invoke<bool>("IsSameMap", "Frozen Mountain", "FrozenMountain"), Is.True);

        [Test]
        public void BR076_EmptyMapNameNeverMatches() =>
            Assert.That(Invoke<bool>("IsSameMap", "", "ElfForest"), Is.False);

        [TestCase("Collect")]
        [TestCase("Defeat")]
        [TestCase("OpenChest")]
        [TestCase("Interact")]
        public void BR093_SupportedObjectivesAreAutoCompletable(string objectiveType) =>
            Assert.That(Invoke<bool>("IsAutoCompleteQuest", objectiveType), Is.True);

        [Test]
        public void BR093_UnknownObjectiveIsNotAutoCompletable() =>
            Assert.That(Invoke<bool>("IsAutoCompleteQuest", "PayToWin"), Is.False);

        [Test]
        public void BR077_ObjectIdentityIgnoresPunctuationAndCase() =>
            Assert.That(Invoke<string>("NormalizeIdentity", "Natalie's Memory!"), Is.EqualTo("nataliesmemory"));

        [Test]
        public void BR077_TargetMatchingRejectsDangerouslyShortNames() =>
            Assert.That(Invoke<bool>("TargetMatches", "Box", "Quest.Box", "Box"), Is.False);

        [Test]
        public void BR077_TargetMatchesNormalizedObjectKey() =>
            Assert.That(Invoke<bool>("TargetMatches", "Natalie's Memory", "AbandonedCastle.NataliesMemory", "Memory"), Is.True);

        [TestCase("Main Quest", true)]
        [TestCase("main_quest", true)]
        [TestCase("Side", false)]
        public void BR092_MainQuestClassificationUsesNormalizedType(string questType, bool expected)
        {
            var quest = NewQuest();
            Set(quest, "QuestType", questType);
            Assert.That(Invoke<bool>("IsMainQuest", quest), Is.EqualTo(expected));
        }

        [TestCase("InProgress", 0)]
        [TestCase("Completed", 1)]
        [TestCase("NotStarted", 2)]
        [TestCase("Claimed", 3)]
        public void BR092_QuestStatusHasStableProgressionPriority(string status, int expected)
        {
            var quest = NewQuest();
            Set(quest, "Status", status);
            Assert.That(Invoke<int>("QuestStatusPriority", quest), Is.EqualTo(expected));
        }

        private static object NewQuest() => Activator.CreateInstance(RequireType("MysticJourney.API.Models.Response.PlayerQuestResponse"));
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property)!.SetValue(target, value);
        private static T Invoke<T>(string method, params object[] args) => (T)QuestUtils.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, args);
        private static Type RequireType(string name) => Type.GetType(name + ", Assembly-CSharp") ?? throw new AssertionException($"Production type '{name}' was not found in Assembly-CSharp.");
    

        [Test]
        public void BR076_PreferredQuestUsesNormalizedCurrentMapName()
        {
            var worldState = RequireType("WorldState");
            var currentMap = worldState.GetProperty("CurrentMapName", BindingFlags.Public | BindingFlags.Static);
            string previousMap = (string)currentMap!.GetValue(null);

            try
            {
                currentMap.SetValue(null, "FrozenMountain");

                var autumnQuest = NewQuest();
                Set(autumnQuest, "QuestId", 20);
                Set(autumnQuest, "QuestType", "Main");
                Set(autumnQuest, "Status", "NotStarted");
                Set(autumnQuest, "MapName", "AutumnPumpkin");

                var frozenQuest = NewQuest();
                Set(frozenQuest, "QuestId", 21);
                Set(frozenQuest, "QuestType", "Main");
                Set(frozenQuest, "Status", "NotStarted");
                Set(frozenQuest, "MapName", "Frozen Mountain");

                Array quests = Array.CreateInstance(autumnQuest.GetType(), 2);
                quests.SetValue(autumnQuest, 0);
                quests.SetValue(frozenQuest, 1);

                object selected = QuestUtils.GetMethod("PickPreferredQuest", BindingFlags.Public | BindingFlags.Static)!
                    .Invoke(null, new object[] { quests });

                Assert.That(selected, Is.Not.Null);
                Assert.That((int)selected.GetType().GetProperty("QuestId")!.GetValue(selected), Is.EqualTo(21));
            }
            finally
            {
                currentMap.SetValue(null, previousMap);
            }
        }
}
}
