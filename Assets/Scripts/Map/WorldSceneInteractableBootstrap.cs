using System;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldSceneInteractableBootstrap
{
    private static readonly HashSet<int> bootstrappedScenes = new();
    private static readonly Vector3 ElderFallbackPosition = new(12.4932f, 18.61223f, 0f);

    public static void EnsureForScene(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, "ElfForest", StringComparison.OrdinalIgnoreCase))
            return;

        ConfigureFallback(scene);

        if (bootstrappedScenes.Contains(scene.handle))
            return;

        bootstrappedScenes.Add(scene.handle);
        RefreshFromApi(scene);
    }

    public static void RefreshFromApi(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, "ElfForest", StringComparison.OrdinalIgnoreCase))
            return;

        if (!ApiClient.Instance.HasToken())
            return;

        WorldApi.Instance.GetState(
            state => ApplyApiState(scene, state),
            error => Debug.LogWarning($"[WorldSceneInteractableBootstrap] GetState failed: {error.Message}")
        );
    }

    private static void ConfigureFallback(Scene scene)
    {
        var elderObject = FindOrCreateElderObject(scene, ElderFallbackPosition);
        if (elderObject != null)
        {
            var elder = elderObject.GetComponent<WorldInteractable>();
            if (elder == null)
                elder = elderObject.AddComponent<WorldInteractable>();
            elder.ConfigureNpc(
                0,
                "Elder Rowan",
                "Tutorial elder and main quest giver.",
                "Chao mung con den ElfLand. Hay noi chuyen voi ta de bat dau hanh trinh.",
                2.75f,
                null
            );
        }

        ConfigureObject(scene, "flower", "ElfForest.WhiteFlower", "White Flower", "Collect", 0, 2.25f);
        ConfigureObject(scene, "Stone", "ElfForest.AncientStoneMarker", "Ancient Stone Marker", "Interact", 0, 2.5f);
    }

    private static void ApplyApiState(Scene scene, WorldStateResponse state)
    {
        if (state == null)
            return;

        var elder = state.Npcs?
            .Where(n => n != null && n.IsActive && string.Equals(n.MapName, "ElfForest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.NPCId)
            .FirstOrDefault(n => string.Equals(n.Name, "Elder Rowan", StringComparison.OrdinalIgnoreCase))
            ?? state.Npcs?.FirstOrDefault(n => n != null && n.IsActive && string.Equals(n.MapName, "ElfForest", StringComparison.OrdinalIgnoreCase));

        var elderPosition = elder != null
            ? new Vector3((float)elder.PositionX, (float)elder.PositionY, 0f)
            : ElderFallbackPosition;
        var elderObject = FindOrCreateElderObject(scene, elderPosition);

        if (elderObject != null && elder != null)
        {
            var interactable = elderObject.GetComponent<WorldInteractable>();
            if (interactable == null)
                interactable = elderObject.AddComponent<WorldInteractable>();
            interactable.ConfigureNpc(
                elder.NPCId,
                elder.Name,
                elder.Description,
                FirstDialogue(elder),
                elder.InteractionRadius > 0f ? elder.InteractionRadius : 2.75f,
                elder.Dialogues?.Where(d => d.LinkedQuestId.HasValue).Select(d => d.LinkedQuestId.Value)
            );
        }

        var flowerQuest = state.Quests?.FirstOrDefault(q =>
            q != null &&
            string.Equals(q.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase) &&
            Contains(q.ObjectiveTarget, "White Flower"));
        if (flowerQuest != null)
            ConfigureObject(scene, "flower", "ElfForest.WhiteFlower", "White Flower", "Collect", flowerQuest.QuestId, 2.25f);

        var stoneQuest = state.Quests?.FirstOrDefault(q =>
            q != null &&
            string.Equals(q.ObjectiveType, "Interact", StringComparison.OrdinalIgnoreCase) &&
            Contains(q.ObjectiveTarget, "Ancient Stone Marker"));
        if (stoneQuest != null)
            ConfigureObject(scene, "Stone", "ElfForest.AncientStoneMarker", "Ancient Stone Marker", "Interact", stoneQuest.QuestId, 2.5f);
    }

    private static GameObject FindOrCreateElderObject(Scene scene, Vector3 position)
    {
        var elder = FindSceneObject(scene, "MageOld");
        if (elder != null)
            return elder;

        elder = FindSceneObject(scene, "ElderRowanInteractable");
        if (elder == null)
        {
            elder = new GameObject("ElderRowanInteractable");
            SceneManager.MoveGameObjectToScene(elder, scene);
            var parent = FindSceneObject(scene, "NPC");
            if (parent != null)
                elder.transform.SetParent(parent.transform, true);
        }

        elder.transform.position = position;
        return elder;
    }

    private static void ConfigureObject(Scene scene, string sceneObjectName, string objectKey, string displayName, string interactionType, int questId, float radius)
    {
        var target = FindSceneObject(scene, sceneObjectName);
        if (target == null)
            return;

        var interactable = target.GetComponent<WorldInteractable>();
        if (interactable == null)
            interactable = target.AddComponent<WorldInteractable>();
        interactable.ConfigureObject(objectKey, displayName, interactionType, questId, 1, radius);
    }

    private static string FirstDialogue(NPCResponse npc)
    {
        return npc?.Dialogues?
            .Where(d => d != null && d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .Select(d => d.Content)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ??
            "Chao mung con den ElfLand. Hay noi chuyen voi ta de bat dau hanh trinh.";
    }

    private static bool Contains(string source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in objects)
        {
            if (obj.name == objectName && obj.scene == scene)
                return obj;
        }

        return null;
    }
}



