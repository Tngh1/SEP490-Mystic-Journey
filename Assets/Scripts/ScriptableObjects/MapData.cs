using UnityEngine;

[CreateAssetMenu(menuName = "Mystic Journey/Map Data")]
public class MapData : ScriptableObject
{
    public int mapId;
    public string mapName;     // Tên Unity scene (ví dụ: "ElfForest")
    public Sprite thumbnail;

    [Header("Quest Chain")]
    public int firstQuestId;   // Quest đầu tiên của map (traverse qua nextQuestId)
    public int unlockQuestId;  // Quest phải Claimed để mở map này (0 = luôn mở)

    [TextArea(2, 4)]
    public string description;
}