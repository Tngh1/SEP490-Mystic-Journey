using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.Core.Utilities;
using MysticJourney.API.Models.Response;

namespace MysticJourney.Features.Quest
{
    // Executes core business logic for mono behaviour.
    public class QuestWaypointManager : MonoBehaviour
    {
        // Executes core business logic for instance.
        public static QuestWaypointManager Instance { get; private set; }

        public MysticJourney.UI.Effects.UIWaypointPointer waypointPointer;
        private Transform playerTransform;

        // Executes core business logic for is tracking enabled.
        public static bool IsTrackingEnabled
        {
            get => PlayerPrefs.GetInt("QuestWaypoint_Enabled", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("QuestWaypoint_Enabled", value ? 1 : 0);
                PlayerPrefs.Save();
                if (Instance != null) Instance.RefreshWaypoint();
            }
        }

        // Initializes internal component caches and dependencies for QuestWaypointManager upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }

            PlayerPrefs.SetInt("QuestWaypoint_Enabled", 1);
            PlayerPrefs.Save();
            Debug.Log("[QuestWaypointManager] Awake: IsTrackingEnabled reset to TRUE");
        }

        // Executes core business logic for is enemy match.
        // Logic details: validates required non-empty string arguments.
        // Returns a boolean indicating operation success.
        private static bool IsEnemyMatch(string enemyGoName, string targetName)
        {
            if (string.IsNullOrWhiteSpace(enemyGoName) || string.IsNullOrWhiteSpace(targetName))
                return false;

            if (targetName.Contains("/"))
            {
                var subTargets = targetName.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var sub in subTargets)
                {
                    if (IsEnemyMatchSingle(enemyGoName, sub))
                        return true;
                }
            }

            return IsEnemyMatchSingle(enemyGoName, targetName);
        }

        // Executes core business logic for is enemy match single.
        // Logic details: validates required non-empty string arguments.
        // Returns a boolean indicating operation success.
        private static bool IsEnemyMatchSingle(string enemyGoName, string targetName)
        {
            if (string.IsNullOrWhiteSpace(enemyGoName) || string.IsNullOrWhiteSpace(targetName))
                return false;

            string cleanGoName = enemyGoName.Replace("(Clone)", "").Trim();

            if (cleanGoName.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                targetName.IndexOf(cleanGoName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string normGo = cleanGoName.Replace('_', ' ').Replace('-', ' ');
            string normTarget = targetName.Replace('_', ' ').Replace('-', ' ');

            if (normGo.IndexOf(normTarget, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                normTarget.IndexOf(normGo, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var targetTokens = normTarget.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var goTokens = normGo.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            var adjectives = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "ice", "fire", "dark", "light", "snow", "poison", "super", "mini", "boss", "spawner", "zone1", "zone2", "zone3"
            };

            var targetNouns = System.Array.FindAll(targetTokens, t => t.Length >= 3 && !adjectives.Contains(t));
            if (targetNouns.Length == 0) targetNouns = System.Array.FindAll(targetTokens, t => t.Length >= 3);

            var goNouns = System.Array.FindAll(goTokens, t => t.Length >= 3 && !adjectives.Contains(t));
            if (goNouns.Length == 0) goNouns = System.Array.FindAll(goTokens, t => t.Length >= 3);

            foreach (var tn in targetNouns)
            {
                foreach (var gn in goNouns)
                {
                    if (gn.IndexOf(tn, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tn.IndexOf(gn, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        // Refresh visible state and subscribe the event handlers required while this component is active.
        private void OnEnable()
        {
            WorldRuntimeEvents.QuestsChanged += RefreshWaypoint;
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(RoutineRefresh());
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDisable()
        {
            WorldRuntimeEvents.QuestsChanged -= RefreshWaypoint;
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Executes core business logic for routine refresh.
        private IEnumerator RoutineRefresh()
        {
            var wait = new WaitForSeconds(2f);
            while (true)
            {
                try { RefreshWaypoint(); }
                catch (System.Exception ex) { Debug.LogWarning($"[QuestWaypointManager] RefreshWaypoint error: {ex.Message}"); }
                yield return wait;
            }
        }

        // Executes core business logic for get target for active quest.
        public Transform GetTargetForActiveQuest()
        {
            if (!IsTrackingEnabled) return null;
            if (QuestUIManager.Instance == null) return null;
            var quests = QuestUIManager.Instance.GetMainQuests();
            var active = MysticJourney.Core.Utilities.QuestUtils.PickPreferredQuest(quests);

            if (active == null) return null;

            playerTransform = GetPlayerTransform();
            return ResolveTarget(active);
        }

        // Executes core business logic for resolve target.
        private Transform ResolveTarget(PlayerQuestResponse active)
        {
            if (QuestUtils.IsStatus(active, "Claimed"))
                return FindMapExit(active.MapName);

            if (QuestUtils.IsQuestOnDifferentMap(active))
            {
                Debug.Log($"[QuestWaypointManager] Quest {active.QuestId} belongs to map '{active.MapName}' but player is on '{WorldState.CurrentMapName}'. Pointing to map exit.");
                return FindMapExit(active.MapName);
            }

            return FindTargetForQuest(active);
        }

        // Executes core business logic for refresh waypoint.
        public void RefreshWaypoint()
        {
            if (!IsTrackingEnabled)
            {
                if (waypointPointer != null) waypointPointer.Clear();
                return;
            }

            if (QuestUIManager.Instance == null) return;
            var quests = QuestUIManager.Instance.GetMainQuests();
            var active = QuestUtils.PickPreferredQuest(quests);

            if (active == null)
            {
                if (waypointPointer != null) waypointPointer.Clear();
                return;
            }

            playerTransform = GetPlayerTransform();
            Transform target = ResolveTarget(active);

            if (target != null && playerTransform != null)
            {
                if (_lastLoggedTarget != target || _lastLoggedQuestId != active.QuestId)
                {
                    _lastLoggedTarget = target;
                    _lastLoggedQuestId = active.QuestId;
                    _lastLoggedNotFound = false;
                    Debug.Log($"[QuestWaypointManager] Found target for quest {active.QuestId}: {target.name} at {target.position}");
                }
                EnsurePointerExists();
                waypointPointer.Setup(target, playerTransform);
            }
            else
            {
                if (!_lastLoggedNotFound || _lastLoggedQuestId != active.QuestId)
                {
                    _lastLoggedTarget = null;
                    _lastLoggedQuestId = active.QuestId;
                    _lastLoggedNotFound = true;
                    Debug.Log($"[QuestWaypointManager] Target not found for quest {active.QuestId}");
                }
                if (waypointPointer != null) waypointPointer.Clear();
            }
        }

        private int _lastLoggedQuestId = -1;
        private Transform _lastLoggedTarget = null;
        private bool _lastLoggedNotFound = false;


        // Executes core business logic for get player transform.
        private Transform GetPlayerTransform()
        {
            if (NetworkPlayer.Local != null)
                return NetworkPlayer.Local.transform;

            var localPe = PlayerEntity.Instance;
            if (localPe != null)
                return localPe.transform;

            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                return p.transform;

            var pe = FindFirstObjectByType<PlayerEntity>();
            if (pe != null)
                return pe.transform;

            return null;
        }

        // Executes core business logic for ensure pointer exists.
        // Logic details: validates required non-empty string arguments.
        private void EnsurePointerExists()
        {
            if (waypointPointer != null) return;

            var go = new GameObject("WaypointPointer");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MysticJourney.UI.Effects.UIWaypointPointer.CreateArrowSprite();
            sr.color = Color.yellow;
            sr.sortingOrder = 9999;
            sr.sortingLayerName = "Default";

            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            var textGo = new GameObject("DistanceText");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0, -0.9f, 0);
            textGo.transform.localScale = new Vector3(0.08f, 0.08f, 1f);

            var tm = textGo.AddComponent<TextMesh>();
            tm.text = "";
            tm.fontSize = 50;
            tm.color = Color.white;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;

            var mr = textGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 10000;

            waypointPointer = go.AddComponent<MysticJourney.UI.Effects.UIWaypointPointer>();
            waypointPointer.arrowRenderer = sr;
            waypointPointer.distanceLabel = tm;
        }

        // Executes core business logic for find quest giver npc.
        // Logic details: validates required non-empty string arguments.
        private Transform FindQuestGiverNpc(int questId, string questGiver, string objectiveTarget = null)
        {
            var interactables = WorldInteractable.All;
            string wantedGiver = (questGiver ?? "").Trim();
            string wantedTarget = (objectiveTarget ?? "").Trim();

            if (questId > 0)
            {
                foreach (var i in interactables)
                {
                    if (i.Kind != WorldInteractableKind.Npc) continue;
                    if (i.LinkedQuestIds != null && System.Linq.Enumerable.Contains(i.LinkedQuestIds, questId))
                        return i.transform;
                }
            }

            if (!string.IsNullOrWhiteSpace(wantedGiver))
            {
                var npc = FindMatchingNpc(interactables, wantedGiver, questId);
                if (npc != null) return npc;
            }

            if (!string.IsNullOrWhiteSpace(wantedTarget))
            {
                var npc = FindMatchingNpc(interactables, wantedTarget, questId);
                if (npc != null) return npc;
            }

            return null;
        }

        // Executes core business logic for clean npc name.
        // Logic details: validates required non-empty string arguments.
        private static string CleanNpcName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string cleaned = name.Trim();
            int idx = cleaned.IndexOf('(');
            if (idx >= 0) cleaned = cleaned.Substring(0, idx);
            return cleaned.Trim();
        }

        // Executes core business logic for find matching npc.
        // Logic details: validates required non-empty string arguments.
        private Transform FindMatchingNpc(System.Collections.Generic.IList<WorldInteractable> interactables, string nameToMatch, int currentQuestId = 0)
        {
            if (string.IsNullOrWhiteSpace(nameToMatch)) return null;
            string cleanTarget = CleanNpcName(nameToMatch);

            WorldInteractable bestMatch = null;
            float minDistance = float.MaxValue;
            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

            foreach (var i in interactables)
            {
                if (i.Kind != WorldInteractableKind.Npc) continue;

                string cleanDisplay = CleanNpcName(i.DisplayName);
                string cleanKey = CleanNpcName(i.ObjectKey);
                string cleanGoName = CleanNpcName(i.gameObject.name);

                bool isExactNameMatch = Matches(cleanDisplay, cleanTarget) || Matches(cleanKey, cleanTarget) || Matches(cleanGoName, cleanTarget);

                if (!isExactNameMatch && currentQuestId > 0 && i.LinkedQuestIds != null && i.LinkedQuestIds.Count > 0 &&
                    !System.Linq.Enumerable.Contains(i.LinkedQuestIds, currentQuestId))
                    continue;

                bool isMatch = isExactNameMatch;

                if (!isMatch && !string.IsNullOrWhiteSpace(cleanDisplay) && !string.IsNullOrWhiteSpace(cleanTarget))
                {
                    if (cleanDisplay.IndexOf(cleanTarget, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cleanTarget.IndexOf(cleanDisplay, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isMatch = true;
                    }
                    else
                    {
                        var targetTokens = cleanTarget.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        var displayTokens = cleanDisplay.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        foreach (var tt in targetTokens)
                        {
                            if (tt.Length < 3) continue;
                            foreach (var dt in displayTokens)
                            {
                                if (dt.Length < 3) continue;
                                if (string.Equals(tt, dt, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    isMatch = true;
                                    break;
                                }
                            }
                            if (isMatch) break;
                        }
                    }
                }

                if (!isMatch && !string.IsNullOrWhiteSpace(cleanGoName) && !string.IsNullOrWhiteSpace(cleanTarget))
                {
                    if (cleanGoName.IndexOf(cleanTarget, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cleanTarget.IndexOf(cleanGoName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        isMatch = true;
                }

                if (isMatch)
                {
                    if (playerTransform != null)
                    {
                        float dist = Vector3.Distance(playerPos, i.transform.position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            bestMatch = i;
                        }
                    }
                    else
                    {
                        return i.transform;
                    }
                }
            }

            if (bestMatch != null) return bestMatch.transform;

            var allGo = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            GameObject bestGo = null;
            float minGoDist = float.MaxValue;

            foreach (var go in allGo)
            {
                if (!go.activeInHierarchy) continue;
                string gName = go.name;

                if (QuestUtils.TargetMatches(cleanTarget, gName, null))
                {
                    if (currentQuestId > 0)
                    {
                        var goInteractable = go.GetComponentInParent<WorldInteractable>();
                        if (goInteractable != null && IsExplicitlyOtherQuest(goInteractable, currentQuestId))
                            continue;
                    }

                    if (playerTransform != null)
                    {
                        float dist = Vector3.Distance(playerPos, go.transform.position);
                        if (dist < minGoDist)
                        {
                            minGoDist = dist;
                            bestGo = go;
                        }
                    }
                    else
                    {
                        bestGo = go;
                        break;
                    }
                }
            }

            if (bestGo != null)
            {
                return bestGo.transform;
            }

            return null;
        }

        // Executes core business logic for is explicitly other quest.
        // Logic details: validates required non-empty string arguments; validates numeric boundary constraints.
        // Returns a boolean indicating operation success.
        private static bool IsExplicitlyOtherQuest(WorldInteractable interactable, int currentQuestId)
        {
            if (currentQuestId <= 0) return false;

            if (interactable.QuestId.HasValue && interactable.QuestId.Value > 0 &&
                interactable.QuestId.Value != currentQuestId)
                return true;

            var linked = interactable.LinkedQuestIds;
            if (linked != null && linked.Count > 0 &&
                !System.Linq.Enumerable.Contains(linked, currentQuestId))
                return true;

            return false;
        }

        // Executes core business logic for matches.
        // Logic details: validates required non-empty string arguments.
        // Returns a boolean indicating operation success.
        private static bool Matches(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
                   string.Equals(a.Trim(), b.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        // Executes core business logic for is available waypoint target.
        // Logic details: validates required non-empty string arguments.
        // Returns a boolean indicating operation success.
        private static bool IsAvailableWaypointTarget(WorldInteractable interactable)
        {
            if (interactable == null || !interactable.gameObject.activeInHierarchy || interactable.InvestigationConsumed)
                return false;

            var collider2D = interactable.GetComponent<UnityEngine.Collider2D>();
            if (collider2D != null && !collider2D.enabled)
                return false;

            var collider = interactable.GetComponent<UnityEngine.Collider>();
            return collider == null || collider.enabled;
        }

        // Executes core business logic for find target for quest.
        private Transform FindTargetForQuest(PlayerQuestResponse quest)
        {
            string objType = quest.ObjectiveType ?? "";
            string targetName = quest.ObjectiveTarget ?? "";
            string questGiver = quest.QuestGiverName ?? "";

            if (objType.Equals("EquipSkill", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Skill", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            bool isTalkQuest = string.Equals(objType, "Talk", System.StringComparison.OrdinalIgnoreCase);

            if (QuestUtils.IsStatus(quest, "NotStarted"))
            {
                var giverNpc = FindQuestGiverNpc(quest.QuestId, questGiver, isTalkQuest ? targetName : null);
                if (giverNpc != null) return giverNpc;
            }

            if (QuestUtils.IsStatus(quest, "Completed"))
            {
                return FindQuestGiverNpc(quest.QuestId, questGiver, isTalkQuest ? targetName : null);
            }


            var interactables = WorldInteractable.All;
            bool skipQuestGiverFallback = false;

            if (objType.Equals("Talk", System.StringComparison.OrdinalIgnoreCase))
            {
                var talkNpc = FindMatchingNpc(interactables, targetName, quest.QuestId);
                if (talkNpc != null) return talkNpc;
            }

            if (objType.Equals("Collect", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Gather", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Fetch", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Interact", System.StringComparison.OrdinalIgnoreCase))
            {
                if (quest.Progress >= Mathf.Max(1, quest.TargetAmount))
                {
                    return FindQuestGiverNpc(quest.QuestId, questGiver, targetName);
                }

                WorldInteractable bestItem = null;
                float minDistance = float.MaxValue;
                Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

                string cleanTarget = (targetName ?? "").Trim();
                if (cleanTarget.IndexOf(" at ", System.StringComparison.OrdinalIgnoreCase) > 0)
                {
                    cleanTarget = cleanTarget.Split(new string[] { " at ", " At ", " AT " }, System.StringSplitOptions.None)[0].Trim();
                }

                for (int pass = 0; pass < 2 && bestItem == null; pass++)
                {
                    minDistance = float.MaxValue;

                    foreach (var i in interactables)
                    {
                        if (i == null || !IsAvailableWaypointTarget(i)) continue;

                        if (i.Kind == WorldInteractableKind.Npc) continue;

                        bool isMatch;
                        if (pass == 0)
                        {
                            isMatch = quest.QuestId > 0 &&
                                      ((i.QuestId.HasValue && i.QuestId.Value == quest.QuestId) ||
                                       (i.LinkedQuestIds != null &&
                                        System.Linq.Enumerable.Contains(i.LinkedQuestIds, quest.QuestId)));
                        }
                        else
                        {
                            isMatch = QuestUtils.TargetMatches(cleanTarget, i.ObjectKey, i.DisplayName) ||
                                      QuestUtils.TargetMatches(cleanTarget, i.gameObject.name, null);
                        }

                        if (isMatch)
                        {
                            float dist = Vector3.Distance(playerPos, i.transform.position);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestItem = i;
                            }
                        }
                    }
                }

                if (bestItem != null) return bestItem.transform;

                var allObjs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                GameObject closestGo = null;
                float minGoDist = float.MaxValue;
                foreach (var go in allObjs)
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    string gName = go.name.Trim();
                    if (string.IsNullOrWhiteSpace(gName)) continue;

                    if (QuestUtils.TargetMatches(cleanTarget, gName, null))
                    {
                        var parentInteractable = go.GetComponentInParent<WorldInteractable>();
                        if (parentInteractable != null && !IsAvailableWaypointTarget(parentInteractable))
                            continue;
                        if (parentInteractable != null && parentInteractable.Kind == WorldInteractableKind.Npc)
                            continue;
                        if (parentInteractable != null && IsExplicitlyOtherQuest(parentInteractable, quest.QuestId))
                            continue;

                        float d = Vector3.Distance(playerPos, go.transform.position);
                        if (d < minGoDist)
                        {
                            minGoDist = d;
                            closestGo = go;
                        }
                    }
                }
                if (closestGo != null) return closestGo.transform;

                skipQuestGiverFallback = true;
            }

            if (objType.Equals("Defeat", System.StringComparison.OrdinalIgnoreCase))
            {
                var allEnemies = Object.FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
                EnemyEntity bestEnemy = null;
                float minEnemyDist = float.MaxValue;
                Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

                for (int pass = 0; pass < 2 && bestEnemy == null; pass++)
                {
                    minEnemyDist = float.MaxValue;

                    foreach (var e in allEnemies)
                    {
                        if (e == null || !e.gameObject.activeInHierarchy) continue;

                        bool isMatch = pass == 0
                            ? QuestUtils.TargetMatches(targetName, e.name, null)
                            : IsEnemyMatch(e.name, targetName);
                        if (!isMatch) continue;

                        float d = Vector3.Distance(playerPos, e.transform.position);
                        if (d < minEnemyDist)
                        {
                            minEnemyDist = d;
                            bestEnemy = e;
                        }
                    }
                }

                if (bestEnemy != null) return bestEnemy.transform;

                var allSceneObjs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                GameObject bestSpawner = null;
                float minSpawnerDist = float.MaxValue;
                for (int pass = 0; pass < 2 && bestSpawner == null; pass++)
                {
                    minSpawnerDist = float.MaxValue;

                    foreach (var go in allSceneObjs)
                    {
                        if (go == null || !go.activeInHierarchy) continue;

                        bool isMatch = pass == 0
                            ? QuestUtils.TargetMatches(targetName, go.name, null)
                            : IsEnemyMatch(go.name, targetName);
                        if (!isMatch) continue;

                        var goInteractable = go.GetComponentInParent<WorldInteractable>();
                        if (goInteractable != null && IsExplicitlyOtherQuest(goInteractable, quest.QuestId))
                            continue;

                        float d = Vector3.Distance(playerPos, go.transform.position);
                        if (d < minSpawnerDist)
                        {
                            minSpawnerDist = d;
                            bestSpawner = go;
                        }
                    }
                }
                if (bestSpawner != null) return bestSpawner.transform;
            }

            if (objType.Equals("Explore", System.StringComparison.OrdinalIgnoreCase) || objType.Equals("Reach", System.StringComparison.OrdinalIgnoreCase))
            {
                var portal = FindPortalToMap(targetName) ?? FindPortalToMap(quest.ObjectiveLocation);
                if (portal == null && targetName.IndexOf("portal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    portal = FindAnyMapPortal();
                if (portal != null) return portal;
            }

            bool isDungeonQuest = (!string.IsNullOrWhiteSpace(targetName) &&
                                   (targetName.IndexOf("Dungeon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    targetName.IndexOf("Temple", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    targetName.IndexOf("Mines", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    targetName.IndexOf("Crypt", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    targetName.IndexOf("Lair", System.StringComparison.OrdinalIgnoreCase) >= 0)) ||
                                  (!string.IsNullOrWhiteSpace(quest.ObjectiveLocation) &&
                                   (quest.ObjectiveLocation.IndexOf("Dungeon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    quest.ObjectiveLocation.IndexOf("Temple", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    quest.ObjectiveLocation.IndexOf("Mines", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    quest.ObjectiveLocation.IndexOf("Crypt", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    quest.ObjectiveLocation.IndexOf("Lair", System.StringComparison.OrdinalIgnoreCase) >= 0));

            if (isDungeonQuest)
            {
                var dungeon = FindDungeonEntrance(interactables);
                if (dungeon != null) return dungeon;
            }

            var locPortal = FindPortalToMap(quest.ObjectiveLocation);
            if (locPortal != null) return locPortal;

            if (skipQuestGiverFallback) return null;
            return FindQuestGiverNpc(quest.QuestId, questGiver, targetName);
        }

        // Executes core business logic for find dungeon entrance.
        private Transform FindDungeonEntrance(System.Collections.Generic.IList<WorldInteractable> interactables)
        {
            foreach (var i in interactables)
            {
                if (i.Kind == WorldInteractableKind.Dungeon)
                    return i.transform;
            }
            return null;
        }


        // Executes core business logic for find map exit.
        private Transform FindMapExit(string destinationMapName)
        {
            var portal = FindPortalToMap(destinationMapName);
            if (portal != null) return portal;

            if ((WorldState.CurrentMapName ?? string.Empty).IndexOf("Autumn", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var boat = FindBoatTransform();
                if (boat != null) return boat;
            }

            return FindAnyMapPortal();
        }

        // Executes core business logic for find portal to map.
        // Logic details: validates required non-empty string arguments.
        private Transform FindPortalToMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName)) return null;
            var portals = FindObjectsByType<MapTeleportPortal>(FindObjectsSortMode.None);
            foreach (var p in portals)
            {
                if (p != null && p.gameObject.activeInHierarchy &&
                    p.targetMapData != null && QuestUtils.IsSameMap(p.targetMapData.mapName, mapName))
                    return p.transform;
            }
            return null;
        }



        // Executes core business logic for find boat transform.
        private Transform FindBoatTransform()
        {
            var boatTeleporter = FindFirstObjectByType<BoatVideoTeleporter>();
            if (boatTeleporter != null && boatTeleporter.gameObject.activeInHierarchy)
                return boatTeleporter.transform;

            var allPortals = FindObjectsByType<MapTeleportPortal>(FindObjectsSortMode.None);
            foreach (var p in allPortals)
            {
                if (p != null && p.gameObject.activeInHierarchy && p.name.IndexOf("Boat", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return p.transform;
            }

            var allObjs = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjs)
            {
                if (go != null && go.activeInHierarchy && (go.name.IndexOf("Boat", System.StringComparison.OrdinalIgnoreCase) >= 0 || go.name.IndexOf("Thuyen", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    return go.transform;
            }

            return null;
        }

        // Executes core business logic for find any map portal.
        private Transform FindAnyMapPortal()
        {
            var portals = FindObjectsByType<MapTeleportPortal>(FindObjectsSortMode.None);
            foreach (var p in portals)
            {
                if (p != null && p.gameObject.activeInHierarchy)
                    return p.transform;
            }

            var interactables = FindObjectsByType<WorldInteractable>(FindObjectsSortMode.None);
            foreach (var i in interactables)
            {
                if (i != null && i.gameObject.activeInHierarchy)
                {
                    var goName = i.gameObject.name;
                    if (goName.IndexOf("Portal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        goName.IndexOf("Gate", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        goName.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return i.transform;
                }
            }

            return null;
        }
    }
}
