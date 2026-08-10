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

            playerTransform = GetPlayerTransform();
            return ResolveTarget(active);
        }

        // Nguồn duy nhất quyết định mũi tên chỉ vào đâu — RefreshWaypoint và
        // GetTargetForActiveQuest phải luôn trả cùng một target.
        private Transform ResolveTarget(PlayerQuestResponse active)
        {
            if (QuestUtils.IsStatus(active, "Claimed"))
                return FindMapExit(active.MapName);

            // Quest tới lượt nằm ở map khác (vd claim xong quest 20 ở AutumnPumpkin thì quest 21
            // ở FrozenMountain): phải chỉ đường RA khỏi map trước. KHÔNG gọi FindTargetForQuest
            // ở đây — fallback 4a của nó bám vào cổng Dungeon có sẵn trên map hiện tại
            // (Abandoned Mines ở AutumnPumpkin) nên nó luôn trả về non-null và mũi tên chỉ vào
            // dungeon thay vì Boat → người chơi không biết đường sang map kế.
            if (QuestUtils.IsQuestOnDifferentMap(active))
            {
                Debug.Log($"[QuestWaypointManager] Quest {active.QuestId} belongs to map '{active.MapName}' but player is on '{WorldState.CurrentMapName}'. Pointing to map exit.");
                return FindMapExit(active.MapName);
            }

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
            var interactables = WorldInteractable.All;
            string wantedGiver = (questGiver ?? "").Trim();
            string wantedTarget = (objectiveTarget ?? "").Trim();

            // 1. Liên kết ĐÍCH DANH trước tiên: WorldSceneInteractableBootstrap đã nạp
            //    LinkedQuestIds từ dialogue của BE (NPCResponse.Dialogues.LinkedQuestId), nên đây
            //    là bằng chứng chắc chắn NPC này giao/nhận quest — không phải phỏng đoán theo tên.
            //    Trước đây khối này nằm CUỐI, nên một NPC trùng tên gần player hơn vẫn thắng.
            if (questId > 0)
            {
                foreach (var i in interactables)
                {
                    if (i.Kind != WorldInteractableKind.Npc) continue;
                    if (i.LinkedQuestIds != null && System.Linq.Enumerable.Contains(i.LinkedQuestIds, questId))
                        return i.transform;
                }
            }

            // 2. Không có liên kết id (NPC chưa được BE gắn dialogue): so tên questGiver.
            if (!string.IsNullOrWhiteSpace(wantedGiver))
            {
                var npc = FindMatchingNpc(interactables, wantedGiver, questId);
                if (npc != null) return npc;
            }

            // 3. Cuối cùng mới tới ObjectiveTarget — chỉ đúng với quest Talk; với quest khác nó là
            //    tên vật/địa điểm nên rất dễ khớp bừa vào NPC.
            if (!string.IsNullOrWhiteSpace(wantedTarget))
            {
                var npc = FindMatchingNpc(interactables, wantedTarget, questId);
                if (npc != null) return npc;
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

        /// <summary>
        /// Tìm NPC khớp tên trong danh sách interactables. currentQuestId dùng để loại bỏ
        /// NPC/object rõ ràng thuộc quest KHÁC (có LinkedQuestIds nhưng không chứa quest hiện tại).
        /// </summary>
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

                // NPC rõ ràng thuộc quest KHÁC: có LinkedQuestIds nhưng không chứa quest hiện tại.
                // Trước đây NPC trùng tên gần player hơn luôn thắng — giờ loại bỏ sớm.
                if (currentQuestId > 0 && i.LinkedQuestIds != null && i.LinkedQuestIds.Count > 0 &&
                    !System.Linq.Enumerable.Contains(i.LinkedQuestIds, currentQuestId))
                    continue;

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

            // --- FALLBACK: Không WorldInteractable nào khớp → quét GameObject trơ trong scene ---
            var allGo = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            GameObject bestGo = null;
            float minGoDist = float.MaxValue;

            foreach (var go in allGo)
            {
                if (!go.activeInHierarchy) continue;
                string gName = go.name;

                // Cùng luật TargetMatches như mọi nơi khác, thay cho IndexOf hai chiều không giới
                // hạn (tên vật nằm trong mục tiêu là đủ khớp → "Tree", "Box" khớp bừa cả scene).
                if (QuestUtils.TargetMatches(cleanTarget, gName, null))
                {
                    // Loại bỏ object rõ ràng thuộc quest KHÁC: nếu có WorldInteractable với
                    // QuestId hoặc LinkedQuestIds trỏ sang quest khác, nó không phải target của ta.
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
                // KHÔNG TỰ Ý THÊM WorldInteractable VÀO QUÁI HOẶC VẬT THỂ BẤT KỲ!
                // Chỉ cần trả về transform để trỏ mũi tên là đủ.
                return bestGo.transform;
            }

            return null;
        }

        /// <summary>
        /// True nếu interactable được gán rõ ràng cho một quest KHÁC currentQuestId.
        /// "Rõ ràng" = có QuestId hoặc LinkedQuestIds trỏ sang quest cụ thể mà quest đó
        /// không phải quest hiện tại. Object không gán quest nào (QuestId=0, LinkedQuestIds rỗng)
        /// KHÔNG bị loại — chúng có thể là target chưa được BE liên kết.
        /// </summary>
        private static bool IsExplicitlyOtherQuest(WorldInteractable interactable, int currentQuestId)
        {
            if (currentQuestId <= 0) return false;

            // Có QuestId rõ ràng khác quest hiện tại
            if (interactable.QuestId.HasValue && interactable.QuestId.Value > 0 &&
                interactable.QuestId.Value != currentQuestId)
                return true;

            // Có LinkedQuestIds nhưng không chứa quest hiện tại
            var linked = interactable.LinkedQuestIds;
            if (linked != null && linked.Count > 0 &&
                !System.Linq.Enumerable.Contains(linked, currentQuestId))
                return true;

            return false;
        }

        private static bool Matches(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
                   string.Equals(a.Trim(), b.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

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

            // KHÔNG hardcode "quest cuối map" theo QuestId ở đây. QuestId bị đánh số lại mỗi lần
            // chèn main quest mới, và 3 khối cũ (16/21/27) đã trỏ sai hẳn: quest 16 giờ là
            // "Trial II: Haunted Quarter" giữa chương, nên vừa giết đủ 10 con là mũi tên bắn ra
            // Thuyền thay vì quay về Arthur. Việc "hết việc ở map này thì đi map khác" đã do
            // ResolveTarget lo: nó so MapName của quest tới lượt với map hiện tại rồi chỉ ra exit.
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
            var interactables = WorldInteractable.All;
            bool skipQuestGiverFallback = false;

            // 1. Talk to NPC
            if (objType.Equals("Talk", System.StringComparison.OrdinalIgnoreCase))
            {
                var talkNpc = FindMatchingNpc(interactables, targetName, quest.QuestId);
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
                Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

                string cleanTarget = (targetName ?? "").Trim();
                if (cleanTarget.IndexOf(" at ", System.StringComparison.OrdinalIgnoreCase) > 0)
                {
                    cleanTarget = cleanTarget.Split(new string[] { " at ", " At ", " AT " }, System.StringSplitOptions.None)[0].Trim();
                }

                // HAI LƯỢT, KHÔNG TRỘN. Trước đây id và tên nằm chung một if/else nên cả hai kiểu
                // khớp cùng đổ vào một vòng "chọn gần nhất": một vật TRÙNG TÊN nhưng không thuộc
                // nhiệm vụ vẫn thắng vật đã được BE gắn đúng questId, chỉ vì nó đứng gần player hơn.
                // Lượt 1 là liên kết đích danh (WorldSceneInteractableBootstrap ghi questId từ API),
                // và chỉ khi lượt 1 trắng tay mới cho phép đoán theo tên ở lượt 2.
                for (int pass = 0; pass < 2 && bestItem == null; pass++)
                {
                    minDistance = float.MaxValue;

                    foreach (var i in interactables)
                    {
                        if (i == null || !IsAvailableWaypointTarget(i)) continue;

                        // Vật cần nhặt không bao giờ là NPC. Phép so tên có chiều ngược (mục tiêu
                        // chứa tên vật), nên NPC "Natalie" khớp với mục tiêu "Natalie's Memory" rồi
                        // thắng ở vòng chọn "gần nhất" khi player đứng cạnh NPC — mũi tên chỉ sai
                        // về NPC thay vì căn nhà chứa vật phẩm.
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
                            // Cùng một luật so khớp với cổng tương tác (PlayerWorldInteractor):
                            // trước đây mỗi bên tự viết luật riêng nên mũi tên chỉ vào vật mà cổng
                            // đó từ chối. TargetMatches có chuẩn hoá + chặn tên quá ngắn.
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

                // If not found in interactables, search scene GameObjects matching cleanTarget
                var allObjs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                GameObject closestGo = null;
                float minGoDist = float.MaxValue;
                foreach (var go in allObjs)
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    string gName = go.name.Trim();
                    if (string.IsNullOrWhiteSpace(gName)) continue;

                    // Lượt quét CẢ SCENE này là nguồn khớp bừa nặng nhất: nó xét mọi GameObject,
                    // kể cả đồ trang trí không có WorldInteractable. Dùng đúng luật TargetMatches
                    // (chuẩn hoá + chặn tên dưới 4 ký tự) thay cho IndexOf hai chiều: trước đây
                    // mục tiêu chứa tên vật là đủ, nên tên ngắn như "Tree"/"Box" khớp hàng loạt.
                    if (QuestUtils.TargetMatches(cleanTarget, gName, null))
                    {
                        var parentInteractable = go.GetComponentInParent<WorldInteractable>();
                        if (parentInteractable != null && !IsAvailableWaypointTarget(parentInteractable))
                            continue;
                        // Cùng lý do như trên: chặn NPC lọt vào qua phép so tên chiều ngược.
                        if (parentInteractable != null && parentInteractable.Kind == WorldInteractableKind.Npc)
                            continue;
                        // Object rõ ràng thuộc quest KHÁC: skip để không chỉ mũi tên vào vật
                        // cùng tên nhưng không phải target của nhiệm vụ hiện tại.
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

                // Chưa thu thập đủ mà không resolve được vật phẩm: KHÔNG được rơi xuống
                // fallback 4c (NPC giao quest) — nếu không mũi tên sẽ nhảy sang NPC khi
                // player đứng gần NPC (NPC gần hơn nên thắng ở bước chọn "gần nhất").
                // Vẫn cho phép dungeon/portal vì vật phẩm có thể nằm ở khu vực khác.
                skipQuestGiverFallback = true;
            }

            // 3. Defeat Monster — boss có thể nằm TRONG dungeon nên không có mặt ở world scene.
            if (objType.Equals("Defeat", System.StringComparison.OrdinalIgnoreCase))
            {
                var allEnemies = Object.FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
                EnemyEntity bestEnemy = null;
                float minEnemyDist = float.MaxValue;
                Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

                // Quái KHÔNG có liên kết đích danh nào để dựa vào: PlayerQuestResponse chỉ có
                // ObjectiveTarget dạng chuỗi, và EnemyEntity.MonsterId của quái đặt sẵn trong
                // scene thường là 0 (chỉ DungeonSpawner mới bơm id). Nên ở đây phải phân TẦNG:
                // lượt 0 chỉ nhận trùng tên khít (chuẩn hoá), lượt 1 mới nới ra IsEnemyMatch
                // (khớp theo token >= 3 ký tự). Trước đây chỉ có lượt nới, nên mục tiêu
                // "Ice Golem" bắt luôn con "Golem" thường đứng gần hơn.
                //
                // ponytail: chỉ hết hẳn nhập nhằng khi BE trả MonsterId trong PlayerQuestResponse
                // — lúc đó so e.MonsterId == quest.MonsterId ở lượt 0 và bỏ hẳn lượt so tên.
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

                // Fallback: Tìm Spawner hoặc GameObject trong scene khớp tên quái (vd DragonIceSpawner_Zone1).
                // Cũng chia hai lượt như trên: lượt quét CẢ SCENE này xét cả đồ trang trí không có
                // EnemyEntity, nên nếu để IsEnemyMatch (token >= 3 ký tự) chạy ngay thì mục tiêu
                // "Ice Dragon" khớp luôn một tảng băng trang trí tên "IceRock" đứng gần hơn.
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

                        // Object rõ ràng thuộc quest KHÁC: skip. Ví dụ cùng tên "Golem" nhưng
                        // một con gắn cho quest 15, quest hiện tại là 20 → chỉ lấy con đúng.
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

            // 4b. Portal tới map/khu vực nhắc trong ObjectiveLocation.
            var locPortal = FindPortalToMap(quest.ObjectiveLocation);
            if (locPortal != null) return locPortal;

            // 4c. Cuối cùng: NPC giao/nhận quest — trừ khi mục tiêu là vật phẩm chưa thu thập đủ.
            if (skipQuestGiverFallback) return null;
            return FindQuestGiverNpc(quest.QuestId, questGiver, targetName);
        }

        private Transform FindDungeonEntrance(System.Collections.Generic.IList<WorldInteractable> interactables)
        {
            foreach (var i in interactables)
            {
                if (i.Kind == WorldInteractableKind.Dungeon)
                    return i.transform;
            }
            return null;
        }

        // NormalizeMapName / IsSameMap / IsQuestOnDifferentMap nằm ở QuestUtils (dùng chung với
        // MainQuestPanelRuntime để tracker và mũi tên luôn nói cùng một chuyện).

        // Đường RA khỏi map hiện tại để đi tới destinationMap. Trên AutumnPumpkin, exit là chiếc
        // Boat — BoatVideoTeleporter kế thừa MapTeleportPortal nên FindPortalToMap tự tìm ra nó.
        private Transform FindMapExit(string destinationMapName)
        {
            var portal = FindPortalToMap(destinationMapName);
            if (portal != null) return portal;

            // Boat chỉ là exit hợp lệ khi player đang ở AutumnPumpkin (map có thuyền đi Frozen).
            // Ở FrozenMountain và các map khác, BoatA là thuyền đến (arrival) — không phải exit.
            if ((WorldState.CurrentMapName ?? string.Empty).IndexOf("Autumn", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var boat = FindBoatTransform();
                if (boat != null) return boat;
            }

            return FindAnyMapPortal();
        }

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
