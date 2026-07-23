using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.Core.Utilities;
using MysticJourney.API.Models.Response;

namespace MysticJourney.Features.Quest
{
    public class QuestWaypointManager : MonoBehaviour
    {
        public static QuestWaypointManager Instance { get; private set; }

        public MysticJourney.UI.Effects.UIWaypointPointer waypointPointer;
        private Transform playerTransform;

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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        private void OnEnable()
        {
            WorldRuntimeEvents.QuestsChanged += RefreshWaypoint;
            StartCoroutine(RoutineRefresh());
        }

        private void OnDisable()
        {
            WorldRuntimeEvents.QuestsChanged -= RefreshWaypoint;
        }

        private void OnDestroy()
        {
            // Clear singleton để lần vào map/scene sau tạo lại instance sạch — nếu không,
            // Instance trỏ tới object đã hủy khiến toggle/refresh từ HUD im lặng thất bại.
            if (Instance == this) Instance = null;
        }

        private IEnumerator RoutineRefresh()
        {
            var wait = new WaitForSeconds(2f);
            while (true)
            {
                // Bọc try/catch: nếu RefreshWaypoint ném 1 lần (vd tìm target lỗi), coroutine
                // while(true) sẽ CHẾT vĩnh viễn -> mũi tên mất luôn tới khi vào lại game.
                try { RefreshWaypoint(); }
                catch (System.Exception ex) { Debug.LogWarning($"[QuestWaypointManager] RefreshWaypoint error: {ex.Message}"); }
                yield return wait;
            }
        }

        public void RefreshWaypoint()
        {
            if (!IsTrackingEnabled)
            {
                if (waypointPointer != null) waypointPointer.Clear();
                return;
            }

            if (QuestManager.Instance == null) return;
            var quests = QuestManager.Instance.GetMainQuests();
            var active = QuestUtils.PickPreferredQuest(quests);

            if (active == null || QuestUtils.IsStatus(active, "Claimed"))
            {
                if (waypointPointer != null) waypointPointer.Clear();
                return;
            }

            playerTransform = GetPlayerTransform();

            Transform target = FindTargetForQuest(active);

            if (target != null && playerTransform != null)
            {
                Debug.Log($"[QuestWaypointManager] Found target for quest {active.QuestId}: {target.name} at {target.position}, player at {playerTransform.position}");
                EnsurePointerExists();
                waypointPointer.Setup(target, playerTransform);
            }
            else
            {
                Debug.Log($"[QuestWaypointManager] Target not found for quest {active.QuestId} with objective {active.ObjectiveType} and target {active.ObjectiveTarget}");
                if (waypointPointer != null) waypointPointer.Clear();
            }
        }

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

            var pe = FindObjectOfType<PlayerEntity>();
            if (pe != null)
                return pe.transform;

            return null;
        }

        private void EnsurePointerExists()
        {
            if (waypointPointer != null) return;

            var go = new GameObject("WaypointPointer");

            // --- Mũi tên (SpriteRenderer) ---
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MysticJourney.UI.Effects.UIWaypointPointer.CreateArrowSprite();
            sr.color = Color.yellow;
            sr.sortingOrder = 9999; // Hiện lên trên tất cả
            sr.sortingLayerName = "Default";

            // Mũi tên to, dễ thấy (0.7f)
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            // --- Chữ khoảng cách (TextMesh) ---
            var textGo = new GameObject("DistanceText");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0, -0.9f, 0);
            textGo.transform.localScale = new Vector3(0.08f, 0.08f, 1f); // Tỉ lệ chữ vừa chuẩn

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

        // Tìm NPC giao/nhận nhiệm vụ theo questId, tên questGiver hoặc objectiveTarget (vd Mysterious Figure, Lyra, Tristan, Arthur).
        private Transform FindQuestGiverNpc(int questId, string questGiver, string objectiveTarget = null)
        {
            var interactables = FindObjectsOfType<WorldInteractable>();
            string wantedGiver = (questGiver ?? "").Trim();
            string wantedTarget = (objectiveTarget ?? "").Trim();

            // 1. First Priority: Try matching specified quest giver NPC by name (e.g., "Elder Rowan", "Fa", "Tristan", "Arthur")
            if (!string.IsNullOrWhiteSpace(wantedGiver))
            {
                var npc = FindMatchingNpc(interactables, wantedGiver);
                if (npc != null) return npc;
            }

            // 2. Second Priority: Try matching objective target NPC name (e.g., "Elder Rowan", "Tristan", "Arthur")
            if (!string.IsNullOrWhiteSpace(wantedTarget))
            {
                var npc = FindMatchingNpc(interactables, wantedTarget);
                if (npc != null) return npc;
            }

            // 3. Fallback: Check if any NPC contains questId in LinkedQuestIds
            if (questId > 0)
            {
                foreach (var i in interactables)
                {
                    if (i.Kind != WorldInteractableKind.Npc) continue;
                    if (i.LinkedQuestIds != null && System.Linq.Enumerable.Contains(i.LinkedQuestIds, questId))
                        return i.transform;
                }
            }

            return null;
        }

        private static string CleanNpcName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string cleaned = name.Trim();
            int idx = cleaned.IndexOf('(');
            if (idx >= 0) cleaned = cleaned.Substring(0, idx);
            return cleaned.Trim();
        }

        private Transform FindMatchingNpc(WorldInteractable[] interactables, string nameToMatch)
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

                bool isMatch = Matches(cleanDisplay, cleanTarget) || Matches(cleanKey, cleanTarget) || Matches(cleanGoName, cleanTarget);

                if (!isMatch && !string.IsNullOrWhiteSpace(cleanDisplay) && !string.IsNullOrWhiteSpace(cleanTarget))
                {
                    if (cleanDisplay.IndexOf(cleanTarget, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cleanTarget.IndexOf(cleanDisplay, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        isMatch = true;
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

            // --- FALLBACK: Nếu không có WorldInteractable nào khớp, tìm tất cả GameObject trong scene khớp MageOld / Elder Rowan ---
            var allGo = FindObjectsOfType<GameObject>();
            GameObject bestGo = null;
            float minGoDist = float.MaxValue;

            foreach (var go in allGo)
            {
                if (!go.activeInHierarchy) continue;
                string gName = go.name;

                bool isGoMatch = false;
                if (!string.IsNullOrWhiteSpace(cleanTarget))
                {
                    if (gName.IndexOf(cleanTarget, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cleanTarget.IndexOf(gName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isGoMatch = true;
                    }
                    else if (cleanTarget.IndexOf("Elder Rowan", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                             (gName.IndexOf("MageOld", System.StringComparison.OrdinalIgnoreCase) >= 0 || gName.IndexOf("Elder", System.StringComparison.OrdinalIgnoreCase) >= 0 || gName.IndexOf("Rowan", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        isGoMatch = true;
                    }
                }

                if (isGoMatch)
                {
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
                var interactable = bestGo.GetComponent<WorldInteractable>();
                if (interactable == null)
                {
                    interactable = bestGo.AddComponent<WorldInteractable>();
                    interactable.ConfigureNpc(0, nameToMatch, "Quest NPC", "Xin chào lữ khách!", 2.5f, null);
                }
                return bestGo.transform;
            }

            return null;
        }

        private static bool Matches(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
                   string.Equals(a.Trim(), b.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        private Transform FindTargetForQuest(PlayerQuestResponse quest)
        {
            string objType = quest.ObjectiveType ?? "";
            string targetName = quest.ObjectiveTarget ?? "";
            string questGiver = quest.QuestGiverName ?? "";

            // UI-only objectives (EquipSkill / Skill) do not require world navigation arrow.
            if (objType.Equals("EquipSkill", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Skill", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // 1. Nếu nhiệm vụ chưa nhận (NotStarted): chỉ đường tới NPC giao quest (Quest Giver). Không tìm targetName để tránh nhảy sang bước sau.
            if (QuestUtils.IsStatus(quest, "NotStarted"))
            {
                return FindQuestGiverNpc(quest.QuestId, questGiver, objectiveTarget: null);
            }

            // 2. Nếu nhiệm vụ đã xong chờ trả (Completed): chỉ đường tới NPC trả quest.
            if (QuestUtils.IsStatus(quest, "Completed"))
            {
                // [HARDCODE HACK]: Nếu là quest tìm xác, ép chỉ đường về Tristan thay vì Arthur (do Database có thể set nhầm Giver)
                if (targetName != null && (targetName.IndexOf("Corpse", System.StringComparison.OrdinalIgnoreCase) >= 0 || targetName.IndexOf("Xác", System.StringComparison.OrdinalIgnoreCase) >= 0 || quest.QuestId == 13))
                {
                    questGiver = "Tristan";
                }
                // Gọi đúng thứ tự: (questId, questGiver, objectiveTarget)
                return FindQuestGiverNpc(quest.QuestId, questGiver, targetName);
            }

            // Quest đang InProgress: thử resolve target cụ thể theo ObjectiveType.
            var interactables = FindObjectsOfType<WorldInteractable>();

            // 1. Talk to NPC
            if (objType.Equals("Talk", System.StringComparison.OrdinalIgnoreCase))
            {
                var talkNpc = FindMatchingNpc(interactables, targetName);
                if (talkNpc != null) return talkNpc;
            }

            // 2. Collect Item or Interact Object
            if (objType.Equals("Collect", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Gather", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Fetch", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Interact", System.StringComparison.OrdinalIgnoreCase))
            {
                // Nếu đã thu thập đủ, mũi tên chuyển sang chỉ tới NPC trả quest
                if (quest.Progress >= Mathf.Max(1, quest.TargetAmount))
                {
                    // [HARDCODE HACK]: Nếu là quest tìm xác, ép chỉ đường về Tristan thay vì Arthur (do Database có thể set nhầm Giver)
                    if (targetName != null && (targetName.IndexOf("Corpse", System.StringComparison.OrdinalIgnoreCase) >= 0 || targetName.IndexOf("Xác", System.StringComparison.OrdinalIgnoreCase) >= 0 || quest.QuestId == 13))
                    {
                        questGiver = "Tristan";
                    }
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

                foreach (var i in interactables)
                {
                    bool isCollectable = (i.Kind == WorldInteractableKind.QuestItem || i.Kind == WorldInteractableKind.Object);
                    if (!isCollectable) continue;

                    // Bỏ qua các object đã bị tắt Collider (nghĩa là đã tương tác xong)
                    var col2D = i.GetComponent<UnityEngine.Collider2D>();
                    var col = i.GetComponent<UnityEngine.Collider>();
                    if ((col2D != null && !col2D.enabled) || (col != null && !col.enabled))
                        continue;

                    bool isMatch = false;
                    if (i.QuestId.HasValue && i.QuestId.Value == quest.QuestId && quest.QuestId > 0)
                    {
                        isMatch = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(cleanTarget))
                    {
                        if (Matches(i.ObjectKey, cleanTarget) || Matches(i.DisplayName, cleanTarget) ||
                            Matches(i.gameObject.name, cleanTarget) ||
                            (!string.IsNullOrWhiteSpace(i.ObjectKey) && cleanTarget.IndexOf(i.ObjectKey.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrWhiteSpace(i.DisplayName) && cleanTarget.IndexOf(i.DisplayName.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrWhiteSpace(i.gameObject.name) && cleanTarget.IndexOf(i.gameObject.name.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            isMatch = true;
                        }
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

                if (bestItem != null) return bestItem.transform;

                // If not found in interactables, search scene GameObjects matching cleanTarget
                var allObjs = FindObjectsOfType<GameObject>();
                GameObject closestGo = null;
                float minGoDist = float.MaxValue;
                foreach (var go in allObjs)
                {
                    if (go.activeInHierarchy && !string.IsNullOrWhiteSpace(cleanTarget) &&
                        go.name.IndexOf(cleanTarget, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        float d = Vector3.Distance(playerPos, go.transform.position);
                        if (d < minGoDist)
                        {
                            minGoDist = d;
                            closestGo = go;
                        }
                    }
                }
                if (closestGo != null) return closestGo.transform;
            }

            // 3. Defeat Monster — boss có thể nằm TRONG dungeon nên không có mặt ở world scene.
            //    Nếu không tìm thấy enemy khớp -> rơi xuống fallback (cổng dungeon), không clear.
            if (objType.Equals("Defeat", System.StringComparison.OrdinalIgnoreCase))
            {
                var allEnemies = FindObjectsOfType<EnemyEntity>();
                foreach (var e in allEnemies)
                {
                    if (e.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return e.transform;
                }
            }

            // 4. Go to Map/Region -> portal tới map đích.
            if (objType.Equals("Explore", System.StringComparison.OrdinalIgnoreCase) || objType.Equals("Reach", System.StringComparison.OrdinalIgnoreCase))
            {
                var portal = FindPortalToMap(targetName);
                if (portal != null) return portal;
            }

            // --- Fallback chain: không resolve được target cụ thể (vd boss trong dungeon chưa
            //     spawn). Dẫn người chơi tới bước đi HỢP LÝ tiếp theo thay vì để mũi tên biến mất.
            // 4a. Cổng Dungeon (khi boss/mục tiêu nằm trong dungeon — user gắn "Dungeon Entrance").
            var dungeon = FindDungeonEntrance(interactables);
            if (dungeon != null) return dungeon;

            // 4b. Portal tới map/khu vực nhắc trong ObjectiveLocation.
            var locPortal = FindPortalToMap(quest.ObjectiveLocation);
            if (locPortal != null) return locPortal;

            // 4c. Cuối cùng: NPC giao/nhận quest.
            return FindQuestGiverNpc(quest.QuestId, questGiver, targetName);
        }

        private Transform FindDungeonEntrance(WorldInteractable[] interactables)
        {
            foreach (var i in interactables)
            {
                if (i.Kind == WorldInteractableKind.Dungeon)
                    return i.transform;
            }
            return null;
        }

        private Transform FindPortalToMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName)) return null;
            var portals = FindObjectsOfType<MapTeleportPortal>();
            foreach (var p in portals)
            {
                if (p.targetMapData != null && p.targetMapData.mapName.IndexOf(mapName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return p.transform;
            }
            return null;
        }
    }
}
