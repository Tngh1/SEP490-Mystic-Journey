using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;
using UnityEngine;

// Executes mono behaviour operation.
public class PlayerWorldInteractor : MonoBehaviour
{
    [SerializeField] private float scanInterval = 0.35f;

    private readonly List<WorldInteractable> interactables = new();
    private WorldInteractable current;
    private float nextScanTime;
    private float nextInteractTime;

    private GameplayInputProvider _input;

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        WorldRuntimeEvents.QuestsChanged += RefreshSceneLinks;
        RefreshSceneLinks();
        RefreshInteractables();
    }

    // Executes resolve input operation.
    private GameplayInputProvider ResolveInput()
    {
        if (_input == null)
        {
            _input = GetComponent<GameplayInputProvider>();
            if (_input == null) _input = GetComponentInParent<GameplayInputProvider>();
        }
        return _input;
    }

    // Per-frame update loop for PlayerWorldInteractor.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
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

        var input = ResolveInput();
        if (input != null && input.InteractPressed)
            HandleInteract();
    }

    // Executes handle interact operation.
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

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshSceneLinks;
        WorldInteractionPromptRuntime.Hide();
    }

    // Executes try interact operation.
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


    // Executes try interact world object operation.
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
    // Executes interact with object operation.
    private void InteractWithObject(WorldInteractable target)
    {
        if (target == null)
            return;

        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[PlayerWorldInteractor] Cannot interact without API token.");
            return;
        }

        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
        {
            target.OnSuccessfulInteraction();
            return;
        }

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

            if (target.QuestId.HasValue && inProgressQuests.Any(q => q.QuestId == target.QuestId.Value))
            {
                questIdToSend = target.QuestId;
            }
            else
            {
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
                var currentState = QuestUIManager.Instance.GetQuestState(quest.QuestId);
                var currentProgress = currentState != null ? currentState.progress : quest.Progress;
                var targetAmount = currentState != null && currentState.targetAmount > 0
                    ? currentState.targetAmount
                    : Mathf.Max(1, quest.TargetAmount);

                if (currentProgress >= targetAmount)
                {
                    // The collection objective is complete; do not call the interaction API again.
                    WorldInteractionPromptRuntime.Hide();
                    return;
                }

                QuestUIManager.Instance.AddProgress(quest.QuestId, target.ProgressDelta);
                var localState = QuestUIManager.Instance.GetQuestState(quest.QuestId);
                targetAmount = localState != null && localState.targetAmount > 0
                    ? localState.targetAmount
                    : Mathf.Max(1, quest.TargetAmount);

                if (localState == null || localState.progress < targetAmount)
                {
                    target.OnSuccessfulInteraction();
                    return;
                }

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

    // Executes find nearest interactable operation.
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

    // Executes is npc reachable operation.
    private static bool IsNpcReachable(WorldInteractable npc)
    {
        var linked = npc.LinkedQuestIds;
        if (linked == null || linked.Count == 0)
            return true;

        var manager = QuestUIManager.Instance;
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

    // Executes name matches operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    private static bool NameMatches(string questName, string displayName, string goName)
    {
        if (string.IsNullOrWhiteSpace(questName)) return false;
        var wanted = questName.Trim();
        return (!string.IsNullOrWhiteSpace(displayName) &&
                string.Equals(displayName.Trim(), wanted, System.StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(goName) &&
                goName.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }


    // Executes is world object reachable operation.
    private static bool IsWorldObjectReachable(WorldInteractable item)
    {
        if (item.GetComponent<IvyTreeInteractable>() != null ||
            item.GetComponent<LockedBridgeGate>() != null ||
            item.GetComponent<DiggingInteractable>() != null ||
            item.GetComponent<OriginTreeInteractable>() != null ||
            item.GetComponent<BoatVideoTeleporter>() != null ||
            item.GetComponent<MapTeleportPortal>() != null)
            return true;

        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return true;

        var manager = QuestUIManager.Instance;
        if (manager == null)
            return true;

        var responses = manager.GetAllResponses();
        if (responses == null || responses.Count == 0)
            return true;

        var governed = false;

        var linked = item.LinkedQuestIds;
        if (linked != null)
        {
            foreach (var questId in linked)
            {
                if (questId <= 0)
                    continue;

                governed = true;
                if (responses.TryGetValue(questId, out var linkedQuest) &&
                    IsQuestAvailableForWorldObject(manager, linkedQuest))
                    return true;
            }
        }

        foreach (var quest in responses.Values)
        {
            if (!QuestUtils.IsWorldObjective(quest))
                continue;

            if (!QuestUtils.TargetMatches(quest.ObjectiveTarget, item.ObjectKey, item.DisplayName))
                continue;

            governed = true;
            if (IsQuestAvailableForWorldObject(manager, quest))
                return true;
        }

        return !governed;
    }

    // Checks whether an active world objective still needs interaction progress.
    private static bool IsQuestAvailableForWorldObject(QuestUIManager manager, PlayerQuestResponse quest)
    {
        if (manager == null || quest == null || !QuestUIManager.IsStatus(quest, "InProgress"))
            return false;

        if (!string.Equals(quest.ObjectiveType, "Collect", System.StringComparison.OrdinalIgnoreCase))
            return true;

        var state = manager.GetQuestState(quest.QuestId);
        int progress = state != null ? state.progress : quest.Progress;
        int targetAmount = state != null && state.targetAmount > 0
            ? state.targetAmount
            : Mathf.Max(1, quest.TargetAmount);
        return progress < targetAmount;
    }


    // Executes find nearest world object operation.
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

            var distance = Vector2.Distance(position, item.transform.position);
            if (distance > item.InteractionRadius || distance >= bestDistance)
                continue;

            if (item.InvestigationConsumed)
                continue;

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
    // Update scene links; it updates from api and loads scene at and processes each matching entry.
    private void RefreshSceneLinks()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            WorldSceneInteractableBootstrap.RefreshFromApi(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
        }
    }

    // Executes refresh interactables operation.
    private void RefreshInteractables()
    {
        interactables.Clear();
        var all = WorldInteractable.All;
        for (int i = 0; i < all.Count; i++)
        {
            var item = all[i];
            if (item == null)
                continue;

            if (!item.gameObject.scene.IsValid() || item.gameObject.scene.name == null)
                continue;

            interactables.Add(item);
        }
    }

    // Executes is npc panel open operation.
    // Validates input parameters against null or empty values.
    private static bool IsNpcPanelOpen()
    {
        var panel = MainNpcPanel.Instance != null ? MainNpcPanel.Instance : FindMainNpcPanelRuntime();
        return panel != null && panel.IsOpen;
    }

    // Executes find main npc panel runtime operation.
    // Validates input parameters against null or empty values.
    private static MainNpcPanel FindMainNpcPanelRuntime()
    {
        return Resources.FindObjectsOfTypeAll<MainNpcPanel>()
            .FirstOrDefault(r => r != null && r.gameObject.scene.IsValid() && !string.IsNullOrEmpty(r.gameObject.scene.name));
    }
}
