using UnityEngine;

// Executes scriptable object operation.
[CreateAssetMenu(menuName = "Mystic Journey/Quest Data", fileName = "NewQuestData")]
public class QuestData : ScriptableObject
{
    [Header("Identity")]
    public int questId;
    public string title;
    [TextArea(2, 4)] public string description;
    // Supported quest types: Main, Side, Daily, or Event; the type determines how the quest is grouped and presented.
    public QuestType type;

    [Header("Map & Chain")]
    public int mapId;
    public int nextQuestId;

    [Header("Objective")]
    public int targetAmount;

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

// Executes quest type operation.
public enum QuestType { Main, Side, Daily, Event }
