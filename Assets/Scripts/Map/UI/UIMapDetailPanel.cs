using UnityEngine;

public class UIMapDetailPanel : MonoBehaviour
{
    [SerializeField]
    private MapSceneController mapManager;

    private MapData currentMap;

    public void Setup(MapData mapData)
    {
        currentMap = mapData;
    }

    public void OnEnterMapButton()
    {
        mapManager.EnterMap(currentMap);
    }
}