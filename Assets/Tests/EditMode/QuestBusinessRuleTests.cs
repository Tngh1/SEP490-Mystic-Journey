using System;
using System.Reflection;
using NUnit.Framework;

namespace MysticJourney.Tests.EditMode
{
    // Initializes a new default instance of the QuestBusinessRuleTests class.
    public sealed class QuestBusinessRuleTests
    {
        // Executes quest utils operation.
        private static Type QuestUtils => RequireType("MysticJourney.Core.Utilities.QuestUtils");

        [TestCase("Frozen Mountain", "FrozenMountain")]
        [TestCase("abandoned_castle", "abandonedcastle")]
        [TestCase("Elf-Forest", "ElfForest")]
        // Executes br076_map names are normalized across api and scene formats operation.
        public void BR076_MapNamesAreNormalizedAcrossApiAndSceneFormats(string input, string expected) =>
            Assert.That(Invoke<string>("NormalizeMapName", input), Is.EqualTo(expected).IgnoreCase);

        [Test]
        // Executes br076_equivalent map names compare equal operation.
        public void BR076_EquivalentMapNamesCompareEqual() =>
            Assert.That(Invoke<bool>("IsSameMap", "Frozen Mountain", "FrozenMountain"), Is.True);

        [Test]
        // Executes br076_empty map name never matches operation.
        public void BR076_EmptyMapNameNeverMatches() =>
            Assert.That(Invoke<bool>("IsSameMap", "", "ElfForest"), Is.False);

        [TestCase("Collect")]
        [TestCase("Defeat")]
        [TestCase("OpenChest")]
        [TestCase("Interact")]
        // Executes br093_supported objectives are auto completable operation.
        public void BR093_SupportedObjectivesAreAutoCompletable(string objectiveType) =>
            Assert.That(Invoke<bool>("IsAutoCompleteQuest", objectiveType), Is.True);

        [Test]
        // Executes br093_unknown objective is not auto completable operation.
        public void BR093_UnknownObjectiveIsNotAutoCompletable() =>
            Assert.That(Invoke<bool>("IsAutoCompleteQuest", "PayToWin"), Is.False);

        [Test]
        // Executes br077_object identity ignores punctuation and case operation.
        public void BR077_ObjectIdentityIgnoresPunctuationAndCase() =>
            Assert.That(Invoke<string>("NormalizeIdentity", "Natalie's Memory!"), Is.EqualTo("nataliesmemory"));

        [Test]
        // Executes br077_target matching rejects dangerously short names operation.
        public void BR077_TargetMatchingRejectsDangerouslyShortNames() =>
            Assert.That(Invoke<bool>("TargetMatches", "Box", "Quest.Box", "Box"), Is.False);

        [Test]
        // Executes br077_target matches normalized object key operation.
        public void BR077_TargetMatchesNormalizedObjectKey() =>
            Assert.That(Invoke<bool>("TargetMatches", "Natalie's Memory", "AbandonedCastle.NataliesMemory", "Memory"), Is.True);

        [TestCase("Main Quest", true)]
        [TestCase("main_quest", true)]
        [TestCase("Side", false)]
        // Executes br092_main quest classification uses normalized type operation.
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
        // Executes br092_quest status has stable progression priority operation.
        // Throws an exception if precondition validations fail.
        public void BR092_QuestStatusHasStableProgressionPriority(string status, int expected)
        {
            var quest = NewQuest();
            Set(quest, "Status", status);
            Assert.That(Invoke<int>("QuestStatusPriority", quest), Is.EqualTo(expected));
        }

        // Executes new quest operation.
        // Throws an exception if precondition validations fail.
        private static object NewQuest() => Activator.CreateInstance(RequireType("MysticJourney.API.Models.Response.PlayerQuestResponse"));
        // Executes set operation.
        // Throws an exception if precondition validations fail.
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property)!.SetValue(target, value);
        // Process the supplied values: maps the input discriminator to the corresponding domain value and fallback.
        private static T Invoke<T>(string method, params object[] args) => (T)QuestUtils.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, args);
        // Executes require type operation.
        // Throws an exception if precondition validations fail.
        private static Type RequireType(string name) => Type.GetType(name + ", Assembly-CSharp") ?? throw new AssertionException($"Production type '{name}' was not found in Assembly-CSharp.");


        [Test]
        // Executes br076_preferred quest uses normalized current map name operation.
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
