using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;
using UnityEngine;

public class PlayerWorldInteractor : MonoBehaviour
{
    [SerializeField] private float scanInterval = 0.35f;

    private readonly List<WorldInteractable> interactables = new();
    private WorldInteractable current;
    private float nextScanTime;
    private float nextInteractTime;

    // Single source of truth for input. The Interact key comes from here so it
    // always honours the player's rebinding (e.g. E → X in settings). Lazily
    // resolved because this component is often added to the player at runtime.
    private GameplayInputProvider _input;

    private void OnEnable()
    {
        WorldRuntimeEvents.QuestsChanged += RefreshSceneLinks;
        RefreshSceneLinks();
        RefreshInteractables();
    }

    private GameplayInputProvider ResolveInput()
    {
        if (_input == null)
        {
            _input = GetComponent<GameplayInputProvider>();
            if (_input == null) _input = GetComponentInParent<GameplayInputProvider>();
        }
        return _input;
    }

    private void Update()
    {
        if (IsNpcPanelOpen())
        {
            current = null;
            WorldInteractionPromptRuntime.Hide();
            return;
        }

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            RefreshInteractables();
        }

        current = FindNearestInteractable(WorldInteractableKind.Npc) ??
                  FindNearestInteractable(WorldInteractableKind.Dungeon) ??
                  FindNearestWorldObject();
        if (current != null)
            WorldInteractionPromptRuntime.Show(current.GetPromptText());
        else
            WorldInteractionPromptRuntime.Hide();

        // Single rebindable Interact key drives ALL world interaction (NPC,
        // dungeon entrance, world object). Previously E and P were hardcoded via
        // the legacy Input Manager, which ignored rebinding — that was the
        // "Interact still uses E after rebinding to X" bug.
        var input = ResolveInput();
        if (input != null && input.InteractPressed)
            HandleInteract();
    }

    /// <summary>
    /// Dispatch an interact press to the nearest interactable. Called by the
    /// input poll in <see cref="Update"/> once the rebindable Interact action
    /// fires. Interact is client-local (it opens panels / calls the API, it does
    /// not affect the networked simulation), so both offline and networked
    /// players reach it through this same local poll — there is no network RPC.
    /// </summary>
    private void HandleInteract()
    {
        if (current != null && current.Kind == WorldInteractableKind.Dungeon)
        {
            var entrance = current.GetComponent<DungeonEntrance>();
            if (entrance != null)
                entrance.Interact();
            return;
        }

        if (current != null &&
            (current.Kind == WorldInteractableKind.Object || current.Kind == WorldInteractableKind.QuestItem))
        {
            TryInteractWorldObject();
            return;
        }

        TryInteract(WorldInteractableKind.Npc);
    }

    private void OnDisable()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshSceneLinks;
        WorldInteractionPromptRuntime.Hide();
    }

    private void TryInteract(WorldInteractableKind kind)
    {
        if (IsNpcPanelOpen())
            return;
        if (Time.time < nextInteractTime)
            return;

        var target = FindNearestInteractable(kind);
        if (target == null)
            return;

        nextInteractTime = Time.time + 0.25f;

        if (kind == WorldInteractableKind.Npc)
        {
            var panel = MainNpcPanel.Instance != null ? MainNpcPanel.Instance : FindMainNpcPanelRuntime();
            if (panel != null)
                panel.OpenForNpc(target);
            else
                Debug.LogWarning("[PlayerWorldInteractor] MainNpcPanel not found in Main scene.");
            return;
        }

        InteractWithObject(target);
    }


    private void TryInteractWorldObject()
    {
        if (IsNpcPanelOpen())
            return;
        if (Time.time < nextInteractTime)
            return;

        var target = FindNearestWorldObject();
        if (target == null)
            return;

        nextInteractTime = Time.time + 0.25f;
        InteractWithObject(target);
    }
    private void InteractWithObject(WorldInteractable target)
    {
        if (target == null)
            return;

        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[PlayerWorldInteractor] Cannot interact without API token.");
            return;
        }

        // Entering a dungeon deliberately does NOT push the dungeon scene name to the
        // server (PlayerWorldPositionSync skips saving while IsInDungeon), so the server
        // still has the world map. Every interact from inside a dungeon therefore fails
        // validation with "Player is currently in <world map>, not <dungeon>" — hundreds
        // of 400s in the console. World quest progress can never belong to a dungeon
        // object anyway, so resolve it locally instead of calling the API.
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
        {
            target.OnSuccessfulInteraction();
            return;
        }

        // Ủy quyền hoàn toàn cho các controller tự quản lý tương tác / video / mở khóa
        if (target.GetComponent<IvyTreeInteractable>() != null ||
            target.GetComponent<LockedBridgeGate>() != null ||
            target.GetComponent<DiggingInteractable>() != null ||
            target.GetComponent<OriginTreeInteractable>() != null ||
            target.GetComponent<BoatVideoTeleporter>() != null)
        {
            target.OnSuccessfulInteraction();
            return;
        }

        int? questIdToSend = null;
        if (QuestUIManager.Instance != null)
        {
            var inProgressQuests = QuestUIManager.Instance.GetMainQuests()
                .Where(q => QuestUIManager.IsStatus(q, "InProgress"))
                .ToList();

            // Chỉ gửi questId nếu quest đó ĐANG ở trạng thái InProgress của người chơi
            if (target.QuestId.HasValue && inProgressQuests.Any(q => q.QuestId == target.QuestId.Value))
            {
                questIdToSend = target.QuestId;
            }
            else
            {
                // Tìm quest InProgress phù hợp với objectKey / displayName
                foreach (var q in inProgressQuests)
                {
                    string targetStr = q.ObjectiveTarget ?? "";
                    if (targetStr.IndexOf(target.ObjectKey, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        targetStr.IndexOf(target.DisplayName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        target.ObjectKey.IndexOf(targetStr.Split(' ')[0], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        questIdToSend = q.QuestId;
                        break;
                    }
                }
            }
        }

        var progressDeltaToSend = target.ProgressDelta;
        if (questIdToSend.HasValue && QuestUIManager.Instance != null)
        {
            var quest = QuestUIManager.Instance.GetQuestResponse(questIdToSend.Value);
            if (quest != null && string.Equals(quest.ObjectiveType, "Collect", System.StringComparison.OrdinalIgnoreCase))
            {
                QuestUIManager.Instance.AddProgress(quest.QuestId, target.ProgressDelta);
                var localState = QuestUIManager.Instance.GetQuestState(quest.QuestId);
                var targetAmount = Mathf.Max(1, quest.TargetAmount);

                if (localState == null || localState.progress < targetAmount)
                {
                    target.OnSuccessfulInteraction();
                    return;
                }

                // Chỉ lần nhặt cuối mới gọi BE; gửi toàn bộ target để BE ghi progress và inventory một lần.
                progressDeltaToSend = targetAmount;
            }
        }

        WorldApi.Instance.InteractObject(
            target.ObjectKey,
            target.InteractionType,
            questIdToSend,
            progressDeltaToSend,
            response =>
            {
                // Áp state server trước: scene có thể unload trong lúc request nhưng QuestUIManager
                // sống xuyên scene và vẫn phải nhận lần commit cuối thành công.
                if (response?.Quest != null && QuestUIManager.Instance != null)
                    QuestUIManager.Instance.ApplyServerQuestState(response.Quest);

                if (response != null && response.CollectedItemId.HasValue && response.CollectedItemId.Value > 0)
                    InventoryUIManager.RefreshAny(refreshStats: false);

                WorldRuntimeEvents.RaiseQuestsChanged();
                if (target == null) return;

                Debug.Log($"[PlayerWorldInteractor] Interacted with '{target.DisplayName}'. QuestId: {questIdToSend}. Response: {response?.Message}");
                target.OnSuccessfulInteraction();
            },
            error =>
            {
                Debug.LogWarning($"[PlayerWorldInteractor] InteractObject failed: {error.Message}");
                WorldRuntimeEvents.RaiseMessage(error.Message);
            }
        );
    }

    private WorldInteractable FindNearestInteractable(WorldInteractableKind? kind = null)
    {
        WorldInteractable nearest = null;
        var bestDistance = float.MaxValue;
        var position = transform.position;

        for (int i = 0; i < interactables.Count; i++)
        {
            var item = interactables[i];
            if (item == null || !item.gameObject.activeInHierarchy)
                continue;

            if (kind.HasValue && item.Kind != kind.Value)
                continue;

            // Distance check TRƯỚC TIÊN — nếu ở ngoài bán kính tương tác thì bỏ qua ngay (0ms)
            var distance = Vector2.Distance(position, item.transform.position);
            if (distance > item.InteractionRadius || distance >= bestDistance)
                continue;

            if (item.Kind == WorldInteractableKind.Npc && !IsNpcReachable(item))
                continue;

            nearest = item;
            bestDistance = distance;
        }

        return nearest;
    }

    /// <summary>
    /// An NPC that only carries quests the player has not reached yet must not be
    /// talkable — otherwise the player can walk up to a later chapter's quest giver
    /// and open a dialogue panel that has nothing to offer ("No linked quest
    /// available."). The server is the gate: PlayerQuestService.GetMyQuests only
    /// materialises a PlayerQuest row once the main chain unlocks it, so "the
    /// QuestUIManager has no response for this id" == "quest not reached yet".
    /// NPCs with no linked quest at all stay talkable (flavour/vendor NPCs), and the
    /// QuestsChanged → RefreshSceneLinks loop re-opens the NPC the moment the quest
    /// unlocks.
    /// </summary>
    private static bool IsNpcReachable(WorldInteractable npc)
    {
        var linked = npc.LinkedQuestIds;
        if (linked == null || linked.Count == 0)
            return true;

        var manager = QuestUIManager.Instance;
        // No quest state loaded (offline / still loading): don't lock the player out.
        if (manager == null)
            return true;

        var responses = manager.GetAllResponses();
        if (responses == null || responses.Count == 0)
            return true;

        foreach (var questId in linked)
        {
            if (questId > 0 && responses.ContainsKey(questId))
                return true;
        }

        // The server links a quest to an NPC by dialogue id OR by QuestGiverName /
        // ObjectiveTarget (WorldService.TalkToNpc). LinkedQuestIds only carries the
        // dialogue ids, so an NPC whose reached quest is matched by name would be
        // locked out by the id check alone. Match on name too before refusing.
        var npcName = npc.DisplayName;
        var goName = npc.gameObject.name;
        foreach (var quest in responses.Values)
        {
            if (quest == null) continue;
            if (NameMatches(quest.QuestGiverName, npcName, goName) ||
                NameMatches(quest.ObjectiveTarget, npcName, goName))
                return true;
        }

        return false;
    }

    private static bool NameMatches(string questName, string displayName, string goName)
    {
        if (string.IsNullOrWhiteSpace(questName)) return false;
        var wanted = questName.Trim();
        return (!string.IsNullOrWhiteSpace(displayName) &&
                string.Equals(displayName.Trim(), wanted, System.StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(goName) &&
                goName.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }


    /// <summary>
    /// A quest object must not be usable before the player has actually reached the
    /// quest it belongs to — otherwise a level-1 player can walk past the pumpkin field
    /// and harvest Chapter 2's crop, or examine Chapter 2's corpses, and the progress is
    /// silently thrown away (the server refuses a questId that is not InProgress, so the
    /// item is consumed for nothing).
    ///
    /// The gate is: if we can name a quest that governs this object, that quest has to be
    /// InProgress. Objects no quest claims (dungeon chests, flavour props) stay usable, and
    /// so does everything while quest state is still loading — the same "don't lock the
    /// player out on a missing response" rule <see cref="IsNpcReachable"/> follows.
    ///
    /// Controllers that already run their own quest check are skipped: they answer with a
    /// spoken hint ("Accept the digging quest from Natalie first.") which is better than
    /// silence, and gating them here would swallow it.
    /// </summary>
    private static bool IsWorldObjectReachable(WorldInteractable item)
    {
        if (item.GetComponent<IvyTreeInteractable>() != null ||
            item.GetComponent<LockedBridgeGate>() != null ||
            item.GetComponent<DiggingInteractable>() != null ||
            item.GetComponent<OriginTreeInteractable>() != null ||
            item.GetComponent<BoatVideoTeleporter>() != null ||
            item.GetComponent<MapTeleportPortal>() != null)
            return true;

        // Dungeon interactables (reward chest) are resolved locally, never against world
        // quests — InteractWithObject short-circuits the API for them too.
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return true;

        var manager = QuestUIManager.Instance;
        if (manager == null)
            return true;

        var responses = manager.GetAllResponses();
        if (responses == null || responses.Count == 0)
            return true;

        var governed = false;

        // 1) Explicit link (scene questId / ConfigureQuestItem). A linked id with no
        //    response at all means the main chain has not unlocked that quest yet.
        var linked = item.LinkedQuestIds;
        if (linked != null)
        {
            foreach (var questId in linked)
            {
                if (questId <= 0)
                    continue;

                governed = true;
                if (responses.TryGetValue(questId, out var linkedQuest) &&
                    QuestUIManager.IsStatus(linkedQuest, "InProgress"))
                    return true;
            }
        }

        // 2) Name link. ConfigureTaggedQuestItems rewrites questId from the API and only
        //    matches ObjectiveType "Collect", so an Interact objective (Corpse, Cursed Root)
        //    comes back with questId 0 and would otherwise slip through ungated.
        foreach (var quest in responses.Values)
        {
            if (!QuestUtils.IsWorldObjective(quest))
                continue;

            if (!QuestUtils.TargetMatches(quest.ObjectiveTarget, item.ObjectKey, item.DisplayName))
                continue;

            governed = true;
            if (QuestUIManager.IsStatus(quest, "InProgress"))
                return true;
        }

        return !governed;
    }

    // IsWorldObjective / TargetMatches / Normalize đã chuyển sang QuestUtils để mũi tên
    // (QuestWaypointManager) và cổng tương tác này dùng CÙNG một luật so khớp — trước đây mỗi
    // bên tự viết luật riêng nên mũi tên có thể chỉ vào vật mà cổng này từ chối.

    private WorldInteractable FindNearestWorldObject()
    {
        WorldInteractable nearest = null;
        var bestDistance = float.MaxValue;
        var position = transform.position;

        for (int i = 0; i < interactables.Count; i++)
        {
            var item = interactables[i];
            if (item == null || !item.gameObject.activeInHierarchy)
                continue;

            if (item.Kind != WorldInteractableKind.Object && item.Kind != WorldInteractableKind.QuestItem)
                continue;

            // 1. Distance check TRƯỚC TIÊN — loại bỏ 99.9% vật thể ở xa trước khi chạy GetComponent / Quest check đắt đỏ
            var distance = Vector2.Distance(position, item.transform.position);
            if (distance > item.InteractionRadius || distance >= bestDistance)
                continue;

            // 2. Kiểm tra xác/hộp sọ đã bị khám phá chưa
            if (item.InvestigationConsumed)
                continue;

            // 3. Bỏ qua nếu collider đã bị tắt (đã tương tác xong)
            var col2D = item.GetComponent<UnityEngine.Collider2D>();
            var col = item.GetComponent<UnityEngine.Collider>();
            if ((col2D != null && !col2D.enabled) || (col != null && !col.enabled))
                continue;

            if (!IsWorldObjectReachable(item))
                continue;

            nearest = item;
            bestDistance = distance;
        }

        return nearest;
    }
    private void RefreshSceneLinks()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            WorldSceneInteractableBootstrap.RefreshFromApi(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
        }
    }

    private void RefreshInteractables()
    {
        interactables.Clear();
        var all = WorldInteractable.All;
        for (int i = 0; i < all.Count; i++)
        {
            var item = all[i];
            if (item == null)
                continue;

            // Only add interactables that are part of an actual loaded scene, not prefabs
            if (!item.gameObject.scene.IsValid() || item.gameObject.scene.name == null)
                continue;

            interactables.Add(item);
        }
    }

    private static bool IsNpcPanelOpen()
    {
        var panel = MainNpcPanel.Instance != null ? MainNpcPanel.Instance : FindMainNpcPanelRuntime();
        return panel != null && panel.IsOpen;
    }

    private static MainNpcPanel FindMainNpcPanelRuntime()
    {
        return Resources.FindObjectsOfTypeAll<MainNpcPanel>()
            .FirstOrDefault(r => r != null && r.gameObject.scene.IsValid() && !string.IsNullOrEmpty(r.gameObject.scene.name));
    }
}

