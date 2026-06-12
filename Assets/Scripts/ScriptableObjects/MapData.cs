using UnityEngine;

[CreateAssetMenu(menuName = "Mystic Journey/Map Data")]
public class MapData : ScriptableObject
{
    public int mapId;
    public string mapName;
    public Sprite thumbnail;

    public int unlockQuestId;

    public string description;
}