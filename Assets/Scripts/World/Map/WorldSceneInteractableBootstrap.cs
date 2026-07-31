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

        // UnderKing is the Q39 objective. Keep the scene instance dormant until that quest is
        // actually in progress so killing it early cannot permanently strand the main chain.
        if (string.Equals(scene.name, "AbandonedCastle", StringComparison.OrdinalIgnoreCase))
            SetSceneObjectActive(scene, "UnderKing", false);

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
        var allInteractables = Resources.FindObjectsOfTypeAll<WorldInteractable>();
        var elder = allInteractables.FirstOrDefault(i => i.gameObject.scene == scene && i.Kind == WorldInteractableKind.Npc &&
            (string.Equals(i.DisplayName.Trim(), "Elder Rowan", StringComparison.OrdinalIgnoreCase) ||
             i.gameObject.name.Contains("MageOld", StringComparison.OrdinalIgnoreCase) ||
             i.gameObject.name.Contains("ElderRowan", StringComparison.OrdinalIgnoreCase)));

        if (elder != null)
        {
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
        ConfigureTaggedQuestItems(scene, null);
    }

    private static void ApplyApiState(Scene scene, WorldStateResponse state)
    {
        if (state == null)
            return;

        var allInteractables = Resources.FindObjectsOfTypeAll<WorldInteractable>();
        var mapNpcs = state.Npcs?.Where(n => n != null && n.IsActive && string.Equals(n.MapName, scene.name, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<NPCResponse>();
        var hideNatalie = IsQuestCompletedOrClaimed(state, 33);

        if (string.Equals(scene.name, "AbandonedCastle", StringComparison.OrdinalIgnoreCase))
        {
            var underKingActive = state.Quests?.Any(q =>
                q != null && q.QuestId == 39 &&
                string.Equals(q.Status, "InProgress", StringComparison.OrdinalIgnoreCase)) == true;
            SetSceneObjectActive(scene, "UnderKing", underKingActive);
        }

        // Standardized NPC configuration pipeline for ALL map NPCs
        foreach (var apiNpc in mapNpcs)
        {
            var matches = allInteractables.Where(i => i.gameObject.scene == scene && i.Kind == WorldInteractableKind.Npc &&
                (i.NpcId == apiNpc.NPCId ||
                 string.Equals(i.DisplayName.Trim(), apiNpc.Name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(i.gameObject.name.Trim(), apiNpc.Name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 (string.Equals(apiNpc.Name, "Elder Rowan", StringComparison.OrdinalIgnoreCase) && i.gameObject.name.Contains("MageOld", StringComparison.OrdinalIgnoreCase)))).ToList();

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

        var flowerQuest = state.Quests?.FirstOrDefault(q =>
            q != null &&
            string.Equals(q.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase) &&
            Contains(q.ObjectiveTarget, "White Flower"));
        if (flowerQuest != null)
            ConfigureObject(scene, "flower", "ElfForest.WhiteFlower", "White Flower", "Collect", flowerQuest.QuestId, 2.25f);

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
            // Cổng dịch chuyển (Boat) cũng bị tag "QuestItem" trong scene, nhưng nó KHÔNG phải
            // vật phẩm thu thập: nó tự quản lý vòng đời (ẩn player, chiếu video, đổi scene) và đã
            // được cấu hình sẵn trong scene (kind Object, objectKey "Boat", type Interact).
            // Nếu để chạy tiếp: (1) AddComponent<WorldRespawnable> khiến nhánh respawner trong
            // OnSuccessfulInteraction tắt hết Renderer → thuyền biến mất mà video không chạy;
            // (2) ConfigureQuestItem ghi đè kind/objectKey thành QuestItem/"ElfForest.Boat".
            if (obj.GetComponent<MapTeleportPortal>() != null)
                continue;

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
        return string.IsNullOrWhiteSpace(compact) ? "QuestItem" : compact;
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

    private static void SetSceneObjectActive(Scene scene, string objectName, bool active)
    {
        var target = FindSceneObject(scene, objectName);
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
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



