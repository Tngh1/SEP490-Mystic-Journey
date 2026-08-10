using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIMapDetailPanel : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private MapSceneController mapManager;

    [Header("UI Elements")]
    public TMP_Text mapNameText;
    public TMP_Text mapDescriptionText;
    public Image mapThumbnail;
    public Button closeButton;
    public Button enterButton;

    private MapData currentMap;

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        if (enterButton != null)
        {
            enterButton.onClick.AddListener(OnEnterMapButton);
        }

        if (mapManager == null)
        {
            mapManager = FindFirstObjectByType<MapSceneController>();
        }
    }

    public void Setup(MapData mapData)
    {
        currentMap = mapData;
        if (mapNameText != null) mapNameText.text = mapData.mapName;
        if (mapDescriptionText != null) mapDescriptionText.text = mapData.description;
        if (mapThumbnail != null && mapData.thumbnail != null) mapThumbnail.sprite = mapData.thumbnail;
    }
    public void OnEnterMapButton()
    {
        if (mapManager == null)
        {
            mapManager = FindFirstObjectByType<MapSceneController>();
        }
        
        if (mapManager != null && currentMap != null)
        {
            mapManager.EnterMap(currentMap);
            gameObject.SetActive(false); // Đóng popup sau khi bấm Enter
        }
        else
        {
            Debug.LogWarning("[UIMapDetailPanel] Cannot enter map. MapManager or CurrentMap is null.");
        }
    }
}