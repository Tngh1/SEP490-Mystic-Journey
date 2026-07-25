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
        if (!scene.IsValid())
            return;

        // Xóa sạch các Skeleton bị gán nhầm script WorldInteractable (quái không được làm NPC)
        var interactables = Resources.FindObjectsOfTypeAll<WorldInteractable>();
        foreach (var i in interactables)
        {
            if (i.gameObject.scene == scene && (i.gameObject.name.Contains("Skeleton") || i.DisplayName == "Skeleton"))
            {
                UnityEngine.Object.Destroy(i); // Xóa component bị gán nhầm
            }
        }

        ConfigureFallback(scene);

        if (bootstrappedScenes.Contains(scene.handle))
            return;

        bootstrappedScenes.Add(scene.handle);
        RefreshFromApi(scene);
    }

    public static void RefreshFromApi(Scene scene)
    {
        if (!scene.IsValid())
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
        ConfigureTaggedQuestItems(scene, null);
    }

    private static void ApplyApiState(Scene scene, WorldStateResponse state)
    {
        if (state == null)
            return;

        var allInteractables = Resources.FindObjectsOfTypeAll<WorldInteractable>();
        var mapNpcs = state.Npcs?.Where(n => n != null && n.IsActive && string.Equals(n.MapName, scene.name, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<NPCResponse>();
        var hideNatalie = IsQuestCompletedOrClaimed(state, 24);

        // 1. Cập nhật tất cả các NPC đã được đặt sẵn hoặc sinh tự động trong Map dựa theo DisplayName & NpcId
        foreach (var apiNpc in mapNpcs)
        {
            var matches = allInteractables.Where(i => i.gameObject.scene == scene && i.Kind == WorldInteractableKind.Npc &&
                (i.NpcId == apiNpc.NPCId ||
                 string.Equals(i.DisplayName.Trim(), apiNpc.Name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(i.gameObject.name.Trim(), apiNpc.Name.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();

            if (hideNatalie && string.Equals(apiNpc.Name, "Natalie", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var match in matches)
                    UnityEngine.Object.Destroy(match.gameObject);
                continue;
            }

            foreach (var match in matches)
            {
                match.ConfigureNpc(
                    apiNpc.NPCId,
                    apiNpc.Name,
                    apiNpc.Description,
                    FirstDialogue(apiNpc),
                    apiNpc.InteractionRadius > 0f ? apiNpc.InteractionRadius : match.InteractionRadius,
                    apiNpc.Dialogues?.Where(d => d.LinkedQuestId.HasValue).Select(d => d.LinkedQuestId.Value)
                );
            }
        }

        // 2. Fallback riêng cho Elder Rowan ở Map 1 & 2 (bảo toàn logic cũ của ElfForest/AutumnPumpkin)
        if (string.Equals(scene.name, "ElfForest", StringComparison.OrdinalIgnoreCase) || string.Equals(scene.name, "AutumnPumpkin", StringComparison.OrdinalIgnoreCase))
        {
            var elder = mapNpcs.OrderBy(n => n.NPCId).FirstOrDefault(n => string.Equals(n.Name, "Elder Rowan", StringComparison.OrdinalIgnoreCase) || string.Equals(n.Name, "Elder Rowan (Pumpkin)", StringComparison.OrdinalIgnoreCase)) ?? mapNpcs.FirstOrDefault();
            
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

        ConfigureTaggedQuestItems(scene, state);
        ConfigureRespawnableCollectItems(scene, state);
    }

    private static bool IsQuestCompletedOrClaimed(WorldStateResponse state, int questId)
    {
        return state?.Quests != null && state.Quests.Any(q =>
            q != null && q.QuestId == questId &&
            (string.Equals(q.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(q.Status, "Claimed", StringComparison.OrdinalIgnoreCase)));
    }

    private static GameObject FindOrCreateElderObject(Scene scene, Vector3 position)
    {
        // Phá hủy tất cả các bản sao vô thừa nhận của MageOld hoặc ElderRowanInteractable
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject primaryElder = null;

        foreach (var obj in objects)
        {
            if (obj.scene != scene) continue;

            if (obj.name.Contains("MageOld") || obj.name.Contains("ElderRowanInteractable"))
            {
                if (primaryElder == null && obj.name.StartsWith("MageOld"))
                {
                    primaryElder = obj;
                }
                else
                {
                    // Nếu đã tìm thấy NPC thật rồi mà còn dư đứa nào khác thì tiêu diệt hết!
                    UnityEngine.Object.Destroy(obj);
                }
            }
        }
        
        // Quét toàn bộ text trong scene để diệt chữ ảo (phòng hờ kẹt trong UI rác)
        var allTexts = Resources.FindObjectsOfTypeAll<TMPro.TextMeshProUGUI>();
        foreach (var t in allTexts)
        {
            if (t.gameObject.scene == scene && t.text != null && (t.text.Contains("Elder Rowan") || t.text.Contains("nawoR")))
            {
                if (primaryElder != null && !t.transform.IsChildOf(primaryElder.transform))
                {
                    var parentCanvas = t.GetComponentInParent<Canvas>();
                    if (parentCanvas != null) UnityEngine.Object.Destroy(parentCanvas.gameObject);
                    else UnityEngine.Object.Destroy(t.gameObject);
                }
            }
        }

        if (primaryElder != null)
        {
            return primaryElder;
        }

        var elder = new GameObject("ElderRowanInteractable");
        SceneManager.MoveGameObjectToScene(elder, scene);
        var parent = FindSceneObject(scene, "NPC");
        if (parent != null)
            elder.transform.SetParent(parent.transform, true);

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


    private static void ConfigureTaggedQuestItems(Scene scene, WorldStateResponse state)
    {
        foreach (var obj in FindSceneObjectsByTag(scene, "QuestItem"))
        {
            var itemName = ResolveQuestItemName(obj.name);
            var objectKey = BuildObjectKey(itemName);
            var questId = FindCollectQuestId(state, itemName);
            var interactable = obj.GetComponent<WorldInteractable>();
            if (interactable == null)
                interactable = obj.AddComponent<WorldInteractable>();

            // Tự động thêm WorldRespawnable để item tự ẩn và mọc lại (giống Pumpkin)
            var respawner = obj.GetComponent<WorldRespawnable>();
            if (respawner == null)
                respawner = obj.AddComponent<WorldRespawnable>();

            interactable.ConfigureQuestItem(objectKey, itemName, questId, 1, 2.25f);
        }
    }

    private static void ConfigureRespawnableCollectItems(Scene scene, WorldStateResponse state)
    {
        foreach (var respawnable in Resources.FindObjectsOfTypeAll<WorldRespawnable>())
        {
            if (respawnable == null || respawnable.gameObject.scene != scene)
                continue;

            var interactable = respawnable.GetComponent<WorldInteractable>();
            if (interactable == null)
                interactable = respawnable.gameObject.AddComponent<WorldInteractable>();

            if (interactable.QuestId.HasValue && interactable.QuestId.Value > 0)
                continue;

            var itemName = ResolveQuestItemName(respawnable.gameObject.name);
            var questId = FindCollectQuestId(state, itemName);
            if (questId <= 0)
                continue;

            var objectKey = BuildObjectKey(itemName);
            interactable.ConfigureQuestItem(objectKey, itemName, questId, 1, 2.25f);
        }
    }

    private static int FindCollectQuestId(WorldStateResponse state, string itemName)
    {
        var quest = state?.Quests?.FirstOrDefault(q =>
            q != null &&
            string.Equals(q.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase) &&
            (Contains(q.ObjectiveTarget, itemName) || Contains(itemName, q.ObjectiveTarget)));
        return quest?.QuestId ?? 0;
    }

    private static string ResolveQuestItemName(string objectName)
    {
        var pretty = PrettyName(objectName);
        if (Contains(pretty, "Branch") || Contains(pretty, "Willow") || Contains(pretty, "Canh"))
            return "Old Willow Branch";
        return Contains(pretty, "Flower") ? "White Flower" : pretty;
    }

    private static string BuildObjectKey(string displayName)
    {
        var compact = PrettyName(displayName).Replace(" ", string.Empty);
        return string.IsNullOrWhiteSpace(compact) ? "ElfForest.QuestItem" : $"ElfForest.{compact}";
    }

    private static string PrettyName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return "Quest Item";

        return objectName
            .Replace("(Clone)", string.Empty)
            .Replace("_", " ")
            .Trim();
    }

    private static IEnumerable<GameObject> FindSceneObjectsByTag(Scene scene, string tag)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in objects)
        {
            if (obj == null || obj.scene != scene)
                continue;

            if (string.Equals(obj.tag, tag, StringComparison.OrdinalIgnoreCase))
                yield return obj;
        }
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
        return !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(value) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
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



