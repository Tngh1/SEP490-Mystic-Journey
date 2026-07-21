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

        private IEnumerator RoutineRefresh()
        {
            while (true)
            {
                RefreshWaypoint();
                yield return new WaitForSeconds(2f);
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

        private Transform FindTargetForQuest(PlayerQuestResponse quest)
        {
            string objType = quest.ObjectiveType ?? "";
            string targetName = quest.ObjectiveTarget ?? "";
            string questGiver = quest.QuestGiverName ?? "";

            // Nếu nhiệm vụ chưa nhận (NotStarted) hoặc đã xong chờ trả (Completed), chỉ đường đến NPC giao nhiệm vụ
            if (QuestUtils.IsStatus(quest, "NotStarted") || QuestUtils.IsStatus(quest, "Completed"))
            {
                var interactables = FindObjectsOfType<WorldInteractable>();
                foreach (var i in interactables)
                {
                    if (i.Kind == WorldInteractableKind.Npc && (i.NpcId.ToString() == questGiver || i.DisplayName == questGiver))
                        return i.transform;
                }
            }

            // 1. Talk to NPC
            if (objType.Equals("Talk", System.StringComparison.OrdinalIgnoreCase))
            {
                var interactables = FindObjectsOfType<WorldInteractable>();
                foreach (var i in interactables)
                {
                    if (i.Kind == WorldInteractableKind.Npc && i.NpcId.ToString() == targetName)
                        return i.transform;
                    if (i.Kind == WorldInteractableKind.Npc && i.DisplayName == targetName)
                        return i.transform;
                }
            }

            // 2. Collect Item
            if (objType.Equals("Collect", System.StringComparison.OrdinalIgnoreCase))
            {
                var interactables = FindObjectsOfType<WorldInteractable>();
                foreach (var i in interactables)
                {
                    if (i.Kind == WorldInteractableKind.QuestItem && i.QuestId == quest.QuestId)
                        return i.transform;
                    if (i.Kind == WorldInteractableKind.QuestItem && i.ObjectKey == targetName)
                        return i.transform;
                }
            }

            // 3. Defeat Monster
            if (objType.Equals("Defeat", System.StringComparison.OrdinalIgnoreCase))
            {
                var allEnemies = FindObjectsOfType<EnemyEntity>();
                foreach (var e in allEnemies)
                {
                    if (e.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return e.transform;
                }
            }

            // 4. Go to Map/Region (Fallback to portal if possible)
            if (objType.Equals("Explore", System.StringComparison.OrdinalIgnoreCase) || objType.Equals("Reach", System.StringComparison.OrdinalIgnoreCase))
            {
                var portals = FindObjectsOfType<MapTeleportPortal>();
                foreach (var p in portals)
                {
                    if (p.targetMapData != null && p.targetMapData.mapName.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return p.transform;
                }
            }

            return null;
        }
    }
}
