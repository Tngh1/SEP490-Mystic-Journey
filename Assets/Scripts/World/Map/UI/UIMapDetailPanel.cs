using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Executes mono behaviour operation.
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

    // Performs startup initialization for UIMapDetailPanel on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
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

    // Executes setup operation.
    public void Setup(MapData mapData)
    {
        currentMap = mapData;
        if (mapNameText != null) mapNameText.text = mapData.mapName;
        if (mapDescriptionText != null) mapDescriptionText.text = mapData.description;
        if (mapThumbnail != null && mapData.thumbnail != null) mapThumbnail.sprite = mapData.thumbnail;
    }
    // Executes on enter map button operation.
    public void OnEnterMapButton()
    {
        if (mapManager == null)
        {
            mapManager = FindFirstObjectByType<MapSceneController>();
        }

        if (mapManager != null && currentMap != null)
        {
            mapManager.EnterMap(currentMap);
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[UIMapDetailPanel] Cannot enter map. MapManager or CurrentMap is null.");
        }
    }
}
