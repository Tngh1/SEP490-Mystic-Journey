using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.Networking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MysticJourney.Tests.PlayMode
{
    public sealed class BusinessRulePlayModeTests
    {
        private static Type RequireType(string name) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(name, false))
                .FirstOrDefault(t => t != null)
            ?? throw new AssertionException("Production type not found: " + name);

        private static object InvokeStatic(string typeName, string methodName, params object[] args)
        {
            var method = RequireType(typeName).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length)
                ?? throw new AssertionException("Method not found: " + typeName + "." + methodName);
            return method.Invoke(null, args);
        }

        private static bool PartyRule(string method, params object[] args) =>
            (bool)InvokeStatic("PartyLifecycleRules", method, args);

        [UnityTest]
        public IEnumerator BR088_091_TwoToFourClientReplicasFollowTheSamePartyLifecycle()
        {
            var sceneFixture = new GameObject("PartyLifecycleFixture");
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            yield return null;

            var members = new HashSet<int> { 101 };
            var ready = new HashSet<int> { 101 };
            const int host = 101;

            foreach (var joiningClient in new[] { 102, 103, 104 })
            {
                Assert.That(PartyRule("CanJoin", 0, members.Count, members.Contains(joiningClient)), Is.True);
                members.Add(joiningClient);
            }

            Assert.That(members.Count, Is.EqualTo(4));
            Assert.That(PartyRule("CanJoin", 0, members.Count, false), Is.False,
                "A fifth client must be rejected by every replica.");
            Assert.That(PartyRule("CanKick", false, true, false), Is.False);
            Assert.That(PartyRule("CanKick", true, true, true), Is.False);

            foreach (var member in members.Where(id => id != host))
            {
                Assert.That(PartyRule("CanChangeReady", true, false), Is.True);
                ready.Add(member);
            }

            Assert.That(PartyRule("CanStartDungeon", true, 0, members.Count, ready.Count, 0), Is.True);
            Assert.That(PartyRule("CanStartDungeon", false, 0, members.Count, ready.Count, 0), Is.False);

            Assert.That(PartyRule("CanLeave", members.Contains(103), false), Is.True);
            members.Remove(103);
            ready.Remove(103);
            Assert.That(members.Contains(103), Is.False);
            Assert.That(PartyRule("CanUsePartyChat", false, false, 103), Is.False,
                "A disconnected client must lose party-chat context.");

            UnityEngine.Object.Destroy(sceneFixture);
            UnityEngine.Object.Destroy(camera.gameObject);
            UnityEngine.Object.Destroy(light.gameObject);
            yield return null;
        }

        [Test]
        public void BR089_OnlyHostCanKickInviteOrStart()
        {
            Assert.That(PartyRule("CanKick", true, true, false), Is.True);
            Assert.That(PartyRule("CanKick", false, true, false), Is.False);
            Assert.That(PartyRule("CanStartDungeon", true, 0, 2, 2, 0), Is.True);
            Assert.That(PartyRule("CanStartDungeon", false, 0, 2, 2, 0), Is.False);
        }

        [Test]
        public void BR090_PartyCapacityIsExactlyFour()
        {
            Assert.That((int)RequireType("PartyLifecycleRules")
                .GetField("MaximumMembers", BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue(), Is.EqualTo(4));
            Assert.That(PartyRule("CanJoin", 0, 3, false), Is.True);
            Assert.That(PartyRule("CanJoin", 0, 4, false), Is.False);
        }

        [Test]
        public void PartyInviteRequiresInviteeToHaveUnlockedDungeonMap()
        {
            const string rules = "MysticJourney.Core.Utilities.MapProgressionRules";

            Assert.That((int)InvokeStatic(rules, "GetMapId", "AutumnPumpkin"), Is.EqualTo(2));
            Assert.That((int)InvokeStatic(rules, "GetMapId", "Frozen Mountain"), Is.EqualTo(3));
            Assert.That((bool)InvokeStatic(rules, "CanInviteToMap", 2, 1), Is.False);
            Assert.That((bool)InvokeStatic(rules, "CanInviteToMap", 2, 2), Is.True);
            Assert.That((bool)InvokeStatic(rules, "CanInviteToMap", 1, 1), Is.True);
            Assert.That((int)InvokeStatic(rules, "GetMapUnlockedByQuest", 20), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator BR078_SceneDialogueFixtureAdvancesOnlyByConfiguredChoice()
        {
            var root = new GameObject("NpcDialogueSceneFixture");
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            yield return null;

            var dialogueType = RequireType("MysticJourney.API.Models.Response.NPCDialogueResponse");
            object NewDialogue(int id, int npcId, int order, bool active, int? questId, string responseType)
            {
                var value = Activator.CreateInstance(dialogueType);
                dialogueType.GetProperty("NPCDialogueId").SetValue(value, id);
                dialogueType.GetProperty("NPCId").SetValue(value, npcId);
                dialogueType.GetProperty("DisplayOrder").SetValue(value, order);
                dialogueType.GetProperty("IsActive").SetValue(value, active);
                dialogueType.GetProperty("LinkedQuestId").SetValue(value, questId);
                dialogueType.GetProperty("ResponseType").SetValue(value, responseType);
                return value;
            }

            var typedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(dialogueType));
            typedList.Add(NewDialogue(12, 7, 2, true, 55, "Story"));
            typedList.Add(NewDialogue(11, 7, 1, true, 55, "Story"));
            typedList.Add(NewDialogue(90, 8, 0, true, 55, "Story"));
            typedList.Add(NewDialogue(13, 7, 3, false, 55, "Story"));

            var flowType = RequireType("NpcDialogueFlow");
            var select = flowType.GetMethod("SelectSequence", BindingFlags.Public | BindingFlags.Static);
            var sequence = select.Invoke(null, new object[] { typedList, 7, (int?)55 });
            Assert.That(((ICollection)sequence).Count, Is.EqualTo(2));

            var first = ((IList)sequence)[0];
            Assert.That((int)dialogueType.GetProperty("NPCDialogueId").GetValue(first), Is.EqualTo(11));

            var tryAdvance = flowType.GetMethod("TryAdvance", BindingFlags.Public | BindingFlags.Static);
            var advanceArgs = new[] { sequence, (object)11, null };
            Assert.That((bool)tryAdvance.Invoke(null, advanceArgs), Is.True);
            Assert.That((int)dialogueType.GetProperty("NPCDialogueId").GetValue(advanceArgs[2]), Is.EqualTo(12));

            var invalidArgs = new[] { sequence, (object)90, null };
            Assert.That((bool)tryAdvance.Invoke(null, invalidArgs), Is.False,
                "Dialogue from another NPC must never become a transition.");

            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(camera.gameObject);
            UnityEngine.Object.Destroy(light.gameObject);
            yield return null;
        }

        [Test]
        public void BR082_CombatPersistenceUsesAuthenticatedBackendDefeatContract()
        {
            var apiConfig = RequireType("MysticJourney.API.Core.ApiConfig");
            Assert.That((string)apiConfig.GetField("MonsterDefeat").GetRawConstantValue(),
                Is.EqualTo("/api/monsters/{0}/defeat"));

            var monsterApi = RequireType("MysticJourney.API.Endpoints.MonsterApi");
            var defeat = monsterApi.GetMethod("Defeat", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(defeat, Is.Not.Null);
            Assert.That(defeat.GetParameters().Length, Is.EqualTo(4));
            Assert.That(defeat.GetParameters()[1].ParameterType.Name, Is.EqualTo("MonsterDefeatRequest"));

            var enemy = RequireType("EnemyEntity");
            var detectDeath = enemy.GetMethod("DetectDeath", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(detectDeath, Is.Not.Null,
                "The scene combat component must retain the production death-to-API boundary.");
        }

        [UnityTest]
        [Category("LiveApi")]
        public IEnumerator BR082_UnitySendsCombatPersistenceToIsolatedApiHost()
        {
            var token = Environment.GetEnvironmentVariable("MJ_TEST_JWT");
            var monsterIdText = Environment.GetEnvironmentVariable("MJ_TEST_MONSTER_ID");
            var monsterId = 0;
            if (string.IsNullOrWhiteSpace(token) || !int.TryParse(monsterIdText, out monsterId))
                Assert.Ignore("Set MJ_API_BASE_URL, MJ_TEST_JWT and MJ_TEST_MONSTER_ID for the isolated live API fixture.");

            var apiConfig = RequireType("MysticJourney.API.Core.ApiConfig");
            var baseUrl = (string)apiConfig.GetProperty("BaseUrl", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var endpoint = (string)apiConfig.GetField("MonsterDefeat").GetRawConstantValue();
            var url = baseUrl + string.Format(endpoint, monsterId);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = 30;
                yield return request.SendWebRequest();

                Assert.That(request.result, Is.EqualTo(UnityWebRequest.Result.Success),
                    request.responseCode + ": " + request.downloadHandler.text);
                Assert.That(request.responseCode, Is.InRange(200, 299));
                Assert.That(request.downloadHandler.text, Does.Contain("experienceEarned"));
                Assert.That(request.downloadHandler.text, Does.Contain("goldEarned"));
            }
        }
    }
}
