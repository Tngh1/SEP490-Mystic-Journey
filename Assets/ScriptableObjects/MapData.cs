using UnityEngine;

// Executes scriptable object operation.
[CreateAssetMenu(menuName = "Mystic Journey/Map Data")]
public class MapData : ScriptableObject
{
    public int mapId;
    public string mapName;
    public Sprite thumbnail;

    [Header("Quest Chain")]
    public int firstQuestId;
    public int unlockQuestId;

    [TextArea(2, 4)]
    public string description;
}
