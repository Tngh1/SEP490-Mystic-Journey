using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWorldInteractor : MonoBehaviour
{
    [SerializeField] private float scanInterval = 0.35f;

    private readonly List<WorldInteractable> interactables = new();
    private WorldInteractable current;
    private float nextScanTime;
    private float nextInteractTime;

    private void OnEnable()
    {
        WorldRuntimeEvents.QuestsChanged += RefreshSceneLinks;
        RefreshSceneLinks();
        RefreshInteractables();
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

        current = FindNearestInteractable(WorldInteractableKind.Npc) ?? FindNearestInteractable(WorldInteractableKind.Object);
        if (current != null)
            WorldInteractionPromptRuntime.Show(current.GetPromptText());
        else
            WorldInteractionPromptRuntime.Hide();

        if (Input.GetKeyDown(KeyCode.E))
            TryInteract(WorldInteractableKind.Npc);

        if (Input.GetKeyDown(KeyCode.P))
            TryInteract(WorldInteractableKind.Object);
    }

    private void OnDisable()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshSceneLinks;
        WorldInteractionPromptRuntime.Hide();
    }

    public void OnInteract(InputValue value)
    {
        if (value != null && !value.isPressed)
            return;
        if (IsNpcPanelOpen())
            return;

        TryInteract(WorldInteractableKind.Npc);
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

    private void InteractWithObject(WorldInteractable target)
    {
        if (target == null)
            return;

        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[PlayerWorldInteractor] Cannot interact without API token.");
            return;
        }

        if (!target.QuestId.HasValue)
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

    private void RefreshSceneLinks()
    {
        WorldSceneInteractableBootstrap.RefreshFromApi(gameObject.scene);
    }

    private void RefreshInteractables()
    {
        interactables.Clear();
        var scene = gameObject.scene;
        var found = Resources.FindObjectsOfTypeAll<WorldInteractable>();
        foreach (var item in found)
        {
            if (item == null || item.gameObject.scene != scene)
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
            .FirstOrDefault(r => r != null && r.gameObject.scene.IsValid() && r.gameObject.scene.name == "Main");
    }
}

