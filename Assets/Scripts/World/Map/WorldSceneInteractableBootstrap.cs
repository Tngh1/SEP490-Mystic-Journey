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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        bootstrappedScenes.Clear();
    }

    public static void EnsureForScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        // Xóa sạch các Skeleton bị gán nhầm script WorldInteractable (quái không được làm NPC)
        var interactables = WorldInteractable.All;
        for (int i = interactables.Count - 1; i >= 0; i--)
        {
            var item = interactables[i];
            if (item == null || item.gameObject.scene != scene)
                continue;

            // Q36's fourth Warden Relic uses the decorative Skeleton_b prop, not an enemy.
            // ponytail: Remove the name fallback once quest items are identified before this cleanup by tag or kind.
            if (item.gameObject.CompareTag("QuestItem") ||
                item.Kind == WorldInteractableKind.QuestItem ||
                item.gameObject.name.StartsWith("Warden Relic", StringComparison.OrdinalIgnoreCase))
                continue;

            if (item.gameObject.name.Contains("Skeleton") || item.DisplayName == "Skeleton")
            {
                UnityEngine.Object.Destroy(item); // Xóa component bị gán nhầm
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
        var allInteractables = WorldInteractable.All;
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

        // "flower" chỉ tồn tại trong ElfForest.unity. Không gate theo scene thì lookup này chạy ở
        // mọi world scene (Main, AbandonedCastle, ...) và luôn log warning "no objects found".
        if (IsElfForest(scene))
            ConfigureObject(scene, "flower", "ElfForest.WhiteFlower", "White Flower", "Collect", 0, 2.25f);
        ConfigureTaggedQuestItems(scene, null);
    }

    private static void ApplyApiState(Scene scene, WorldStateResponse state)
    {
        if (state == null)
            return;

        var allInteractables = WorldInteractable.All;
        var mapNpcs = state.Npcs?.Where(n => n != null && n.IsActive && string.Equals(n.MapName, scene.name, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<NPCResponse>();
        // Chỉ ẩn Natalie khi quest 33 đã "Claimed", KHÔNG phải "Completed".
        // Quest 33 ("[Chapter 4] Lay Natalie to Rest") có ObjectiveTarget = "Ivy Tree" nhưng
        // QuestGiverName = "Natalie": mục tiêu hoàn thành ở Ivy Tree, còn phần trả nhiệm vụ
        // (AutoClaimCompletedQuest trong MainNpcPanelRuntime) phải nói chuyện với Natalie.
        // BatchUpdateProgress flip Status = "Completed" ngay khi chạm Ivy Tree
        // (PlayerQuestService.BatchUpdateProgress), rồi RaiseQuestsChanged gọi lại hàm này
        // → ẩn Natalie TRƯỚC khi người chơi kịp trả nhiệm vụ: "làm xong quest của NPC thì
        // không interact được với NPC đó nữa". Gate theo "Claimed" giữ Natalie sống đúng
        // một lượt nói chuyện cuối.
        var hideNatalie = IsQuestClaimed(state, 33);

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
            // Bật/tắt Natalie TRƯỚC khi lọc `matches`.
            //
            // SetActive thay vì Destroy: hàm này chạy lại mỗi lần QuestsChanged (kể cả mỗi lần đóng
            // panel NPC), nên Destroy là một chiều — bắn sai một lần là Natalie mất hẳn tới khi load
            // lại scene. SetActive(false) còn cứu được vì FindSceneObject dùng
            // Resources.FindObjectsOfTypeAll nên vẫn thấy object đang tắt.
            //
            // Phải đặt trên `matches`: WorldInteractable.OnDisable gỡ object khỏi
            // WorldInteractable.All, nên lúc Natalie đang tắt thì `allInteractables` không chứa cô.
            // Bật lại rồi mới lọc thì SetActive(true) → OnEnable → có mặt trong All ngay trong lần
            // refresh này, khỏi phải chờ refresh sau mới được ConfigureNpc.
            if (string.Equals(apiNpc.Name, "Natalie", StringComparison.OrdinalIgnoreCase))
            {
                SetSceneObjectActive(scene, apiNpc.Name, !hideNatalie);
                if (hideNatalie)
                    continue;
            }

            var matches = allInteractables.Where(i => i.gameObject.scene == scene && i.Kind == WorldInteractableKind.Npc &&
                (i.NpcId == apiNpc.NPCId ||
                 string.Equals(i.DisplayName.Trim(), apiNpc.Name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(i.gameObject.name.Trim(), apiNpc.Name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 (string.Equals(apiNpc.Name, "Elder Rowan", StringComparison.OrdinalIgnoreCase) && i.gameObject.name.Contains("MageOld", StringComparison.OrdinalIgnoreCase)))).ToList();

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
        if (flowerQuest != null && IsElfForest(scene))
            ConfigureObject(scene, "flower", "ElfForest.WhiteFlower", "White Flower", "Collect", flowerQuest.QuestId, 2.25f);

        var wardenQuest = state.Quests?.FirstOrDefault(q =>
            q != null &&
            string.Equals(q.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase) &&
            Contains(q.ObjectiveTarget, "Warden Relic"));
        if (wardenQuest != null)
            ConfigureQuestItemByName(scene, "Warden Relic", "AbandonedCastle.WardenRelic", "Warden Relic", "Collect", wardenQuest.QuestId, 2.25f);

        var aderynQuest = state.Quests?.FirstOrDefault(q =>
            q != null &&
            string.Equals(q.ObjectiveType, "Interact", StringComparison.OrdinalIgnoreCase) &&
            Contains(q.ObjectiveTarget, "Aderyn Memory"));
        if (aderynQuest != null)
            ConfigureQuestItemByName(scene, "Aderyn Memory", "AbandonedCastle.AderynMemory", "Aderyn Memory", "Interact", aderynQuest.QuestId, 2.25f);

        ConfigureTaggedQuestItems(scene, state);
        ConfigureRespawnableCollectItems(scene, state);
    }

    /// <summary>
    /// "Claimed" là trạng thái duy nhất bảo đảm người chơi ĐÃ trả nhiệm vụ xong.
    /// "Completed" chỉ nói mục tiêu đã đạt (BatchUpdateProgress tự flip khi Progress đủ), phần trả
    /// nhiệm vụ cho NPC thì chưa xảy ra — nên đừng dùng "Completed" để ẩn quest giver.
    /// </summary>
    private static bool IsQuestClaimed(WorldStateResponse state, int questId)
    {
        return state?.Quests != null && state.Quests.Any(q =>
            q != null && q.QuestId == questId &&
            string.Equals(q.Status, "Claimed", StringComparison.OrdinalIgnoreCase));
    }



    private static bool IsElfForest(Scene scene)
    {
        return string.Equals(scene.name, "ElfForest", StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureObject(Scene scene, string sceneObjectName, string objectKey, string displayName, string interactionType, int questId, float radius)
    {
        // Tìm tất cả object có tên chứa sceneObjectName (case-insensitive, partial match).
        // FindSceneObject (exact) hay bị miss khi Unity spawn prefab tạo tên "flower(Clone)",
        // "Flower_1", "WhiteFlower" v.v. -- dẫn đến hoa không bao giờ được configure.
        var targets = FindSceneObjects(scene, sceneObjectName);
        if (targets.Count == 0)
        {
            Debug.LogWarning($"[WorldSceneInteractableBootstrap] ConfigureObject: no objects found matching '{sceneObjectName}' in scene '{scene.name}'");
            return;
        }

        foreach (var target in targets)
        {
            var interactable = target.GetComponent<WorldInteractable>();
            if (interactable == null)
                interactable = target.AddComponent<WorldInteractable>();
            interactable.ConfigureObject(objectKey, displayName, interactionType, questId, 1, radius);
        }
    }

    private static void ConfigureQuestItemByName(Scene scene, string sceneObjectName, string objectKey, string itemName, string interactionType, int questId, float radius)
    {
        var targets = FindSceneObjects(scene, sceneObjectName);
        if (targets.Count == 0) return;

        foreach (var target in targets)
        {
            var interactable = target.GetComponent<WorldInteractable>();
            if (interactable == null)
            {
                interactable = target.AddComponent<WorldInteractable>();
                interactable.GetType().GetField("interactionType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(interactable, interactionType);
            }
            interactable.ConfigureQuestItem(objectKey, itemName, questId, 1, radius);
        }
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

    /// <summary>
    /// Tìm tất cả GameObject trong scene có tên CHỨA <paramref name="nameFragment"/> (case-insensitive).
    /// Trả về exact-match trước rồi mới partial-match để ưu tiên đúng object khi có nhiều kết quả.
    /// Đã sửa lỗi partial match quá lỏng lẻo (trước đây "flower" khớp luôn "FlowersRandom" làm
    /// mũi tên nhiệm vụ chỉ bậy vào các bụi cây trang trí).
    /// </summary>
    private static List<GameObject> FindSceneObjects(Scene scene, string nameFragment)
    {
        var exact   = new List<GameObject>();
        var partial = new List<GameObject>();
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        
        string lowerFragment = nameFragment.ToLowerInvariant();

        foreach (var obj in objects)
        {
            if (obj == null || obj.scene != scene) continue;
            
            string lowerName = obj.name.ToLowerInvariant();

            if (lowerName == lowerFragment)
            {
                exact.Add(obj);
            }
            else if (lowerName.StartsWith(lowerFragment))
            {
                // Chỉ cho phép khớp partial nếu phần đuôi là (Clone) hoặc dạng _1, _2 (do Unity spawn/duplicate).
                // Ngăn chặn "flower" khớp bừa vào "FlowersRandom" hoặc "FlowerPot".
                string remainder = lowerName.Substring(lowerFragment.Length).Trim();
                if (remainder == "(clone)" || 
                    (remainder.StartsWith("_") && int.TryParse(remainder.Substring(1), out _)) ||
                    (remainder.StartsWith("(") && remainder.EndsWith(")") && int.TryParse(remainder.Substring(1, remainder.Length - 2), out _)) ||
                    (remainder.StartsWith(" ") && int.TryParse(remainder.Substring(1), out _)))
                {
                    partial.Add(obj);
                }
            }
        }
        exact.AddRange(partial);
        return exact;
    }
}



