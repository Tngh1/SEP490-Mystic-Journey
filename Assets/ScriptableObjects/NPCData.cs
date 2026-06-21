using UnityEngine;

/// <summary>
/// Dữ liệu tĩnh NPC — không lưu DB, chỉ dùng trong Unity.
/// npcId chỉ dùng để identify trong game, không cần khớp BE.
/// </summary>
[CreateAssetMenu(menuName = "Mystic Journey/NPC Data", fileName = "NewNPCData")]
public class NPCData : ScriptableObject
{
    [Header("Identity")]
    public int npcId;
    public string npcName;
    public string role;         // Ví dụ: "Village Elder", "Blacksmith"
    public Sprite portrait;

    [Header("Greeting")]
    [TextArea(2, 4)]
    public string greetingText; // Lời chào mặc định khi player nói chuyện

    [Header("Quests")]
    public int[] availableQuestIds; // QuestId mà NPC này cung cấp / nhận hoàn thành
}
