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
            else { Destroy(this); return; }

            // Luôn bật tracking khi game khởi động (reset bất kỳ giá trị PlayerPrefs cũ nào)
            PlayerPrefs.SetInt("QuestWaypoint_Enabled", 1);
            PlayerPrefs.Save();
            Debug.Log("[QuestWaypointManager] Awake: IsTrackingEnabled reset to TRUE");
        }

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

        public Transform GetTargetForActiveQuest()
        {
            if (!IsTrackingEnabled) return null;
            if (QuestManager.Instance == null) return null;
            var quests = QuestManager.Instance.GetMainQuests();
            var active = MysticJourney.Core.Utilities.QuestUtils.PickPreferredQuest(quests);

            if (active == null) return null;

            if (MysticJourney.Core.Utilities.QuestUtils.IsStatus(active, "Claimed"))
            {
                // Chỉ dùng boat trên AutumnPumpkin; FrozenMountain và các map khác dùng portal trực tiếp.
                string currentSceneGet = WorldState.CurrentMapName ?? string.Empty;
                bool isOnBoatMapGet = currentSceneGet.IndexOf("Autumn", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isOnBoatMapGet)
                {
                    var boat = FindBoatTransform();
                    if (boat != null) return boat;
                }
                var portal = FindAnyMapPortal();
                if (portal != null) return portal;
                return null;
            }

            playerTransform = GetPlayerTransform();
            return FindTargetForQuest(active);
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

            if (active == null)
            {
                if (waypointPointer != null) waypointPointer.Clear();
                return;
            }

            playerTransform = GetPlayerTransform();
            Transform target = null;

            if (QuestUtils.IsStatus(active, "Claimed"))
            {
                // Boat chỉ là exit hợp lệ khi player đang ở AutumnPumpkin (map có thuyền đi Frozen).
                // Ở FrozenMountain và các map khác, BoatA là thuyền đến (arrival) — không phải exit.
                // Chỉ dùng boat trên AutumnPumpkin; map khác dùng portal/gate trực tiếp.
                string currentSceneClaimed = WorldState.CurrentMapName ?? string.Empty;
                bool isOnBoatMap = currentSceneClaimed.IndexOf("Autumn", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isOnBoatMap)
                    target = FindBoatTransform() ?? FindAnyMapPortal();
                else
                    target = FindAnyMapPortal();
            }
            else
            {
                target = FindTargetForQuest(active);

                // Nếu target không tìm được và quest thuộc map khác (player cần di chuyển sang map mới),
                // chỉ đến portal/gate gần nhất để dẫn đường thay vì để mũi tên biến mất.
                if (target == null && !string.IsNullOrWhiteSpace(active.MapName))
                {
                    string currentScene = WorldState.CurrentMapName ?? string.Empty;
                    bool isQuestOnDifferentMap = currentScene.Length > 0 &&
                        active.MapName.IndexOf(currentScene, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        currentScene.IndexOf(active.MapName, System.StringComparison.OrdinalIgnoreCase) < 0;
                    if (isQuestOnDifferentMap)
                    {
                        Debug.Log($"[QuestWaypointManager] Quest {active.QuestId} belongs to map '{active.MapName}' but player is on '{currentScene}'. Pointing to portal.");
                        target = FindPortalToMap(active.MapName) ?? FindAnyMapPortal();
                    }
                }
            }

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
                // KHÔNG TỰ Ý THÊM WorldInteractable VÀO QUÁI HOẶC VẬT THỂ BẤT KỲ!
                // Chỉ cần trả về transform để trỏ mũi tên là đủ.
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

            // 0a. Xử lý đặc biệt cho Quest 16 (Quest cuối map Autumn / đi thuyền sang FrozenMountain):
            // CHỈ dẫn ra Thuyền SAU KHI lữ khách đã nói chuyện xong với Arthur (Completed/Claimed hoặc Progress >= 1)
            if (quest.QuestId == 16)
            {
                bool isFinishedWithArthur = QuestUtils.IsStatus(quest, "Completed") ||
                                            QuestUtils.IsStatus(quest, "Claimed") ||
                                            quest.Progress >= Mathf.Max(1, quest.TargetAmount);
                if (isFinishedWithArthur)
                {
                    var boat = FindBoatTransform();
                    if (boat != null) return boat;
                }
            }

            // 0b. Xử lý đặc biệt cho Quest 21 (Quest cuối map FrozenMountain — đánh GolemBoss):
            // Sau khi GolemBoss bị giết (Completed/Claimed), chỉ đến portal/gate để thoát FrozenMountain.
            if (quest.QuestId == 21)
            {
                bool isFinishedWithGolem = QuestUtils.IsStatus(quest, "Completed") ||
                                           QuestUtils.IsStatus(quest, "Claimed") ||
                                           quest.Progress >= Mathf.Max(1, quest.TargetAmount);
                if (isFinishedWithGolem)
                {
                    var portal = FindAnyMapPortal();
                    if (portal != null) return portal;
                }
            }

            // 0c. Quest 27 (Ask for the Way Home — quest cuối map AbandonedCastle):
            // khi Completed/Claimed → Elf Guard đã mở portal, chỉ đến portal về ElfForest.
            if (quest.QuestId == 27)
            {
                bool isFinishedWithElfGuard = QuestUtils.IsStatus(quest, "Completed") ||
                                              QuestUtils.IsStatus(quest, "Claimed") ||
                                              quest.Progress >= Mathf.Max(1, quest.TargetAmount);
                if (isFinishedWithElfGuard)
                {
                    // Elf Guard đã mở portal về ElfForest — chỉ đến portal đó.
                    var elfPortal = FindPortalToMap("ElfForest") ?? FindAnyMapPortal();
                    if (elfPortal != null) return elfPortal;
                }
            }

            bool isTalkQuest = string.Equals(objType, "Talk", System.StringComparison.OrdinalIgnoreCase);

            // 1. Nhiệm vụ chưa nhận (NotStarted): luôn chỉ đường tới NPC giao quest, bất kể ObjectiveType —
            // muốn nhận quest thì phải nói chuyện với NPC trước, chưa cần tới mục tiêu.
            // (targetName chỉ dùng cho quest Talk; với Explore/Collect nó là tên vật/địa điểm, không phải NPC.)
            if (QuestUtils.IsStatus(quest, "NotStarted"))
            {
                var giverNpc = FindQuestGiverNpc(quest.QuestId, questGiver, isTalkQuest ? targetName : null);
                if (giverNpc != null) return giverNpc;
            }

            // 2. Nếu nhiệm vụ đã xong chờ trả (Completed): chỉ đường tới NPC trả quest.
            if (QuestUtils.IsStatus(quest, "Completed"))
            {
                return FindQuestGiverNpc(quest.QuestId, questGiver, isTalkQuest ? targetName : null);
            }


            // Quest đang InProgress: thử resolve target cụ thể theo ObjectiveType.
            var interactables = FindObjectsOfType<WorldInteractable>();
            bool skipQuestGiverFallback = false;

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
                    return FindQuestGiverNpc(quest.QuestId, questGiver, targetName);
                }

                WorldInteractable bestItem = null;
                float minDistance = float.MaxValue;
                // Vật phẩm đang tắt Collider (vừa hái, đang chờ WorldRespawnable mọc lại):
                // vẫn là mục tiêu đúng, chỉ ưu tiên sau vật phẩm còn sống.
                WorldInteractable bestHidden = null;
                float minHiddenDistance = float.MaxValue;
                Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

                string cleanTarget = (targetName ?? "").Trim();
                if (cleanTarget.IndexOf(" at ", System.StringComparison.OrdinalIgnoreCase) > 0)
                {
                    cleanTarget = cleanTarget.Split(new string[] { " at ", " At ", " AT " }, System.StringSplitOptions.None)[0].Trim();
                }

                foreach (var i in interactables)
                {
                    if (i == null || !i.gameObject.activeInHierarchy) continue;

                    // Object đã bị tắt Collider = vừa thu thập, đang chờ hồi sinh.
                    var col2D = i.GetComponent<UnityEngine.Collider2D>();
                    var col = i.GetComponent<UnityEngine.Collider>();
                    bool isHidden = (col2D != null && !col2D.enabled) || (col != null && !col.enabled);

                    bool isMatch = false;
                    if (i.QuestId.HasValue && i.QuestId.Value == quest.QuestId && quest.QuestId > 0)
                    {
                        isMatch = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(cleanTarget))
                    {
                        string gName = i.gameObject.name.Trim();
                        string dName = (i.DisplayName ?? "").Trim();
                        string oKey = (i.ObjectKey ?? "").Trim();

                        if (Matches(oKey, cleanTarget) || Matches(dName, cleanTarget) || Matches(gName, cleanTarget) ||
                            (!string.IsNullOrWhiteSpace(oKey) && cleanTarget.IndexOf(oKey, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrWhiteSpace(dName) && cleanTarget.IndexOf(dName, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrWhiteSpace(gName) && cleanTarget.IndexOf(gName, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (cleanTarget.IndexOf("Flower", System.StringComparison.OrdinalIgnoreCase) >= 0 && (gName.IndexOf("flower", System.StringComparison.OrdinalIgnoreCase) >= 0 || dName.IndexOf("flower", System.StringComparison.OrdinalIgnoreCase) >= 0)))
                        {
                            isMatch = true;
                        }
                    }

                    if (isMatch)
                    {
                        float dist = Vector3.Distance(playerPos, i.transform.position);
                        if (isHidden)
                        {
                            if (dist < minHiddenDistance)
                            {
                                minHiddenDistance = dist;
                                bestHidden = i;
                            }
                        }
                        else if (dist < minDistance)
                        {
                            minDistance = dist;
                            bestItem = i;
                        }
                    }
                }

                if (bestItem != null) return bestItem.transform;
                if (bestHidden != null) return bestHidden.transform;

                // If not found in interactables, search scene GameObjects matching cleanTarget
                var allObjs = FindObjectsOfType<GameObject>();
                GameObject closestGo = null;
                float minGoDist = float.MaxValue;
                foreach (var go in allObjs)
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    string gName = go.name.Trim();
                    if (string.IsNullOrWhiteSpace(gName)) continue;

                    bool matchGo = false;
                    if (!string.IsNullOrWhiteSpace(cleanTarget))
                    {
                        matchGo = gName.IndexOf(cleanTarget, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  cleanTarget.IndexOf(gName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  (cleanTarget.IndexOf("Flower", System.StringComparison.OrdinalIgnoreCase) >= 0 && gName.IndexOf("flower", System.StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    if (matchGo)
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

                // Chưa thu thập đủ mà không resolve được vật phẩm: KHÔNG được rơi xuống
                // fallback 4c (NPC giao quest) — nếu không mũi tên sẽ nhảy sang NPC khi
                // player đứng gần NPC (NPC gần hơn nên thắng ở bước chọn "gần nhất").
                // Vẫn cho phép dungeon/portal vì vật phẩm có thể nằm ở khu vực khác.
                skipQuestGiverFallback = true;
            }

            // 3. Defeat Monster — boss có thể nằm TRONG dungeon nên không có mặt ở world scene.
            if (objType.Equals("Defeat", System.StringComparison.OrdinalIgnoreCase))
            {
                var allEnemies = FindObjectsOfType<EnemyEntity>();
                EnemyEntity bestEnemy = null;
                float minEnemyDist = float.MaxValue;
                Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

                foreach (var e in allEnemies)
                {
                    if (e == null || !e.gameObject.activeInHierarchy) continue;
                    if (IsEnemyMatch(e.name, targetName))
                    {
                        float d = Vector3.Distance(playerPos, e.transform.position);
                        if (d < minEnemyDist)
                        {
                            minEnemyDist = d;
                            bestEnemy = e;
                        }
                    }
                }

                if (bestEnemy != null) return bestEnemy.transform;

                // Fallback: Tìm Spawner hoặc GameObject trong scene khớp tên quái (vd DragonIceSpawner_Zone1)
                var allSceneObjs = FindObjectsOfType<GameObject>();
                GameObject bestSpawner = null;
                float minSpawnerDist = float.MaxValue;
                foreach (var go in allSceneObjs)
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    if (IsEnemyMatch(go.name, targetName))
                    {
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

            // 4. Go to Map/Region -> portal tới map đích.
            if (objType.Equals("Explore", System.StringComparison.OrdinalIgnoreCase) || objType.Equals("Reach", System.StringComparison.OrdinalIgnoreCase))
            {
                var portal = FindPortalToMap(targetName) ?? FindPortalToMap(quest.ObjectiveLocation);
                // Target kiểu "Portal"/"Gate" là chính cái cổng, không phải tên map đích -> lấy portal bất kỳ.
                if (portal == null && targetName.IndexOf("portal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    portal = FindAnyMapPortal();
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

            // 4c. Cuối cùng: NPC giao/nhận quest — trừ khi mục tiêu là vật phẩm chưa thu thập đủ.
            if (skipQuestGiverFallback) return null;
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



        private Transform FindMatchingSceneObject(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName)) return null;

            var allSceneObjs = FindObjectsOfType<GameObject>();
            GameObject bestGo = null;
            float minDist = float.MaxValue;
            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

            foreach (var go in allSceneObjs)
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (IsEnemyMatch(go.name, targetName))
                {
                    float d = Vector3.Distance(playerPos, go.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        bestGo = go;
                    }
                }
            }

            return bestGo != null ? bestGo.transform : null;
        }

        private Transform FindBoatTransform()
        {
            var boatTeleporter = FindObjectOfType<BoatVideoTeleporter>();
            if (boatTeleporter != null && boatTeleporter.gameObject.activeInHierarchy)
                return boatTeleporter.transform;

            var allPortals = FindObjectsOfType<MapTeleportPortal>();
            foreach (var p in allPortals)
            {
                if (p != null && p.gameObject.activeInHierarchy && p.name.IndexOf("Boat", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return p.transform;
            }

            var allObjs = FindObjectsOfType<GameObject>();
            foreach (var go in allObjs)
            {
                if (go != null && go.activeInHierarchy && (go.name.IndexOf("Boat", System.StringComparison.OrdinalIgnoreCase) >= 0 || go.name.IndexOf("Thuyen", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    return go.transform;
            }

            return null;
        }

        private Transform FindAnyMapPortal()
        {
            var portals = FindObjectsOfType<MapTeleportPortal>();
            foreach (var p in portals)
            {
                if (p != null && p.gameObject.activeInHierarchy)
                    return p.transform;
            }

            var interactables = FindObjectsOfType<WorldInteractable>();
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
