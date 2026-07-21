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

            if (playerTransform == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            Transform target = FindTargetForQuest(active);

            if (target != null)
            {
                Debug.Log($"[QuestWaypointManager] Found target for quest {active.QuestId}: {target.name} at {target.position}");
                EnsurePointerExists();
                waypointPointer.Setup(target, playerTransform);
            }
            else
            {
                Debug.Log($"[QuestWaypointManager] Target not found for quest {active.QuestId} with objective {active.ObjectiveType} and target {active.ObjectiveTarget}");
                if (waypointPointer != null) waypointPointer.Clear();
            }
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

            // Scale vừa vặn gọn gàng (0.35f)
            go.transform.localScale = new Vector3(0.35f, 0.35f, 1f);

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

        // Tìm NPC giao nhiệm vụ theo tên, khớp "khoan dung": bỏ qua hoa/thường + khoảng trắng,
        // thử cả DisplayName lẫn tên GameObject. Nếu quest không ghi tên người giao thì dùng
        // hằng FallbackQuestGiver ("Elder Rowan"). Nếu vẫn không có tên nào -> NPC đầu tiên trong map.
        private Transform FindQuestGiverNpc(string questGiver)
        {
            var interactables = FindObjectsOfType<WorldInteractable>();
            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

            string wanted = (questGiver ?? "").Trim();
            
            // 1. Try matching specified quest giver name
            if (!string.IsNullOrWhiteSpace(wanted))
            {
                foreach (var i in interactables)
                {
                    if (i.Kind != WorldInteractableKind.Npc) continue;
                    if (Matches(i.DisplayName, wanted) || Matches(i.ObjectKey, wanted) || Matches(i.gameObject.name, wanted) ||
                        (!string.IsNullOrWhiteSpace(i.DisplayName) && i.DisplayName.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrWhiteSpace(i.gameObject.name) && i.gameObject.name.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0))
                        return i.transform;
                }
            }

            // 2. Try known fallback quest givers: "Migra", "Elder Rowan", "Rowan", "MageOld"
            string[] knownGivers = new string[] { "Migra", "Elder Rowan", "Rowan", "MageOld", "Elder" };
            foreach (var g in knownGivers)
            {
                foreach (var i in interactables)
                {
                    if (i.Kind != WorldInteractableKind.Npc) continue;
                    if ((!string.IsNullOrWhiteSpace(i.DisplayName) && i.DisplayName.IndexOf(g, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrWhiteSpace(i.gameObject.name) && i.gameObject.name.IndexOf(g, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrWhiteSpace(i.ObjectKey) && i.ObjectKey.IndexOf(g, System.StringComparison.OrdinalIgnoreCase) >= 0))
                        return i.transform;
                }
            }

            // 3. Fallback: find the NPC closest to player / spawn point
            WorldInteractable closestNpc = null;
            float minDist = float.MaxValue;
            foreach (var i in interactables)
            {
                if (i.Kind != WorldInteractableKind.Npc) continue;
                float dist = Vector3.Distance(playerPos, i.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestNpc = i;
                }
            }

            return closestNpc != null ? closestNpc.transform : null;
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

            // Nếu nhiệm vụ chưa nhận (NotStarted) hoặc đã xong chờ trả (Completed), CHỈ chỉ
            // đường đến NPC giao nhiệm vụ. return luôn (kể cả null) để không rơi xuống logic
            // objective bên dưới — nếu không, quest "Defeat" chưa nhận sẽ chỉ thẳng tới quái.
            if (QuestUtils.IsStatus(quest, "NotStarted") || QuestUtils.IsStatus(quest, "Completed"))
            {
                return FindQuestGiverNpc(questGiver);
            }

            // Quest đang InProgress: thử resolve target cụ thể theo ObjectiveType.
            var interactables = FindObjectsOfType<WorldInteractable>();

            // 1. Talk to NPC
            if (objType.Equals("Talk", System.StringComparison.OrdinalIgnoreCase))
            {
                foreach (var i in interactables)
                {
                    if (i.Kind != WorldInteractableKind.Npc) continue;
                    if (i.NpcId.ToString() == targetName || Matches(i.DisplayName, targetName))
                        return i.transform;
                }
            }

            // 2. Collect Item
            if (objType.Equals("Collect", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Gather", System.StringComparison.OrdinalIgnoreCase) ||
                objType.Equals("Fetch", System.StringComparison.OrdinalIgnoreCase))
            {
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

            // 4c. Cuối cùng: NPC giao quest (đích an toàn, luôn có trên map gốc).
            return FindQuestGiverNpc(questGiver);
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
