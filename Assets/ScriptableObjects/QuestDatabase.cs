using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime lookup database cho tất cả QuestData ScriptableObjects.
/// Được khởi tạo một lần bởi QuestManager → O(1) lookup theo questId.
/// </summary>
[CreateAssetMenu(menuName = "Mystic Journey/Quest Database", fileName = "QuestDatabase")]
public class QuestDatabase : ScriptableObject
{
    [Tooltip("Kéo tất cả QuestData assets vào đây")]
    public List<QuestData> allQuests = new();

    private Dictionary<int, QuestData> _lookup;
    private bool _initialized;

    /// <summary>Gọi một lần từ QuestManager.Awake() để build lookup dict.</summary>
    public void Initialize()
    {
        _lookup = new Dictionary<int, QuestData>(allQuests.Count);
        foreach (var q in allQuests)
        {
            if (q == null) continue;
            if (_lookup.ContainsKey(q.questId))
            {
                Debug.LogWarning($"[QuestDatabase] Duplicate questId={q.questId}, bỏ qua '{q.name}'.");
                continue;
            }
            _lookup[q.questId] = q;
        }
        _initialized = true;
        Debug.Log($"[QuestDatabase] Initialized {_lookup.Count} quests.");
    }

    public QuestData GetById(int questId)
    {
        if (!_initialized) Initialize();
        return _lookup.TryGetValue(questId, out var q) ? q : null;
    }

    public bool Exists(int questId)
    {
        if (!_initialized) Initialize();
        return _lookup.ContainsKey(questId);
    }

    /// <summary>Lấy toàn bộ quest theo chuỗi chain bắt đầu từ firstQuestId.</summary>
    public List<QuestData> GetChain(int firstQuestId, int maxDepth = 100)
    {
        var chain = new List<QuestData>();
        var visited = new HashSet<int>();
        int currentId = firstQuestId;

        for (int i = 0; i < maxDepth; i++)
        {
            if (currentId <= 0 || visited.Contains(currentId)) break;
            var q = GetById(currentId);
            if (q == null) break;

            chain.Add(q);
            visited.Add(currentId);
            currentId = q.nextQuestId;
        }
        return chain;
    }
}
