using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
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
            var panel = MainNpcPanelRuntime.Instance != null ? MainNpcPanelRuntime.Instance : FindMainNpcPanelRuntime();
            if (panel != null)
                panel.OpenForNpc(target);
            else
                Debug.LogWarning("[PlayerWorldInteractor] MainNpcPanelRuntime not found in Main scene.");
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

        if (!target.QuestId.HasValue && target.Kind != WorldInteractableKind.QuestItem)
        {
            WorldSceneInteractableBootstrap.RefreshFromApi(gameObject.scene);
            Debug.LogWarning($"[PlayerWorldInteractor] {target.DisplayName} is not linked to an active quest yet.");
            return;
        }

        WorldApi.Instance.InteractObject(
            target.ObjectKey,
            target.InteractionType,
            target.QuestId,
            target.ProgressDelta,
            response =>
            {
                Debug.Log($"[PlayerWorldInteractor] {target.DisplayName}: {response?.Message ?? "interacted"}");
                WorldRuntimeEvents.RaiseMessage(response?.Message ?? $"{target.DisplayName} interacted.");
                WorldRuntimeEvents.RaiseQuestsChanged();
            },
            error => Debug.LogWarning($"[PlayerWorldInteractor] InteractObject failed: {error.Message}")
        );
    }

    private WorldInteractable FindNearestInteractable(WorldInteractableKind? kind = null)
    {
        WorldInteractable nearest = null;
        var bestDistance = float.MaxValue;
        var position = transform.position;

        foreach (var item in interactables)
        {
            if (item == null || !item.gameObject.activeInHierarchy)
                continue;

            if (kind.HasValue && item.Kind != kind.Value)
                continue;

            var distance = Vector2.Distance(position, item.transform.position);
            if (distance > item.InteractionRadius || distance >= bestDistance)
                continue;

            nearest = item;
            bestDistance = distance;
        }

        return nearest;
    }


    private WorldInteractable FindNearestWorldObject()
    {
        WorldInteractable nearest = null;
        var bestDistance = float.MaxValue;
        var position = transform.position;

        foreach (var item in interactables)
        {
            if (item == null || !item.gameObject.activeInHierarchy)
                continue;

            if (item.Kind != WorldInteractableKind.Object && item.Kind != WorldInteractableKind.QuestItem)
                continue;

            var distance = Vector2.Distance(position, item.transform.position);
            if (distance > item.InteractionRadius || distance >= bestDistance)
                continue;

            nearest = item;
            bestDistance = distance;
        }

        return nearest;
    }
    private void RefreshSceneLinks()
    {
        WorldSceneInteractableBootstrap.RefreshFromApi(gameObject.scene);
    }

    private void RefreshInteractables()
    {
        interactables.Clear();
        var found = Resources.FindObjectsOfTypeAll<WorldInteractable>();
        foreach (var item in found)
        {
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
        var panel = MainNpcPanelRuntime.Instance != null ? MainNpcPanelRuntime.Instance : FindMainNpcPanelRuntime();
        return panel != null && panel.IsOpen;
    }

    private static MainNpcPanelRuntime FindMainNpcPanelRuntime()
    {
        return Resources.FindObjectsOfTypeAll<MainNpcPanelRuntime>()
            .FirstOrDefault(r => r != null && r.gameObject.scene.IsValid() && !string.IsNullOrEmpty(r.gameObject.scene.name));
    }
}

