using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class MainMinimapToggleRuntime : MonoBehaviour
{
    private GameObject miniMap;

    // Performs startup initialization for MainMinimapToggleRuntime on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        miniMap = FindSceneObject("MiniMap");
        if (miniMap != null)
        {
            miniMap.SetActive(false);
            var mapBtn = miniMap.GetComponent<Button>();
            if (mapBtn == null) mapBtn = miniMap.AddComponent<Button>();
            mapBtn.onClick.RemoveListener(OpenMapPanel);
            mapBtn.onClick.AddListener(OpenMapPanel);
        }

        var buttonObject = FindSceneObject("MiniMapButton");
        if (buttonObject != null)
        {
            var button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
            button.onClick.RemoveListener(Toggle);
            button.onClick.AddListener(Toggle);
        }
    }


    // Executes toggle operation.
    private void Toggle()
    {
        if (miniMap == null)
            miniMap = FindSceneObject("MiniMap");

        if (miniMap == null)
            return;

        if (WorldState.PlayerLevel < MainFeatureUnlockRuntime.MiniMapButtonLevel)
        {
            miniMap.SetActive(false);
            return;
        }

        miniMap.SetActive(!miniMap.activeSelf);
    }

    // Executes open map panel operation.
    private void OpenMapPanel()
    {
        if (!MapUIManager.CanOpen) return;

        if (UIManager.Instance != null && UIManager.Instance.mapPanel != null)
        {
            UIManager.Instance.mapPanel.SetActive(true);
        }
    }

    // Executes find scene object operation.
    // Validates input parameters against null or empty values.
    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in objects)
        {
            if (obj.name == objectName && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
                return obj;
        }

        return null;
    }
}
