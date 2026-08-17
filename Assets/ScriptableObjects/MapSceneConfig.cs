using UnityEngine;

// Executes scriptable object operation.
[CreateAssetMenu(menuName = "Mystic Journey/Map Scene Config")]
public class MapSceneConfig : ScriptableObject
{
    public MapData mapData;

    public string sceneName;
}
