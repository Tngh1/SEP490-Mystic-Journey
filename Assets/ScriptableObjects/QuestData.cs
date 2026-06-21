using UnityEngine;

/// <summary>
/// Dữ liệu tĩnh của một quest. Lưu trong Unity, không lưu DB.
/// QuestId phải khớp với QuestId trong bảng PlayerQuests (BE).
/// </summary>
[CreateAssetMenu(menuName = "Mystic Journey/Quest Data", fileName = "NewQuestData")]
public class QuestData : ScriptableObject
{
    [Header("Identity")]
    public int questId;
    public string title;
    [TextArea(2, 4)] public string description;
    public QuestType type;

    [Header("Map & Chain")]
    public int mapId;
    public int nextQuestId; // 0 = quest cuối của map

    [Header("Objective")]
    public int targetAmount; // Số lượng cần hoàn thành

    [Header("Rewards (mirror quest_config.json trên server)")]
    public int rewardGold;
    public int rewardExp;
    public int rewardGems;
    public Sprite rewardItemIcon;
    public string rewardItemName;

    [Header("NPC Giver")]
    public string npcGiverName;
    public Sprite npcGiverPortrait;
}

public enum QuestType { Main, Side, Daily, Event }
