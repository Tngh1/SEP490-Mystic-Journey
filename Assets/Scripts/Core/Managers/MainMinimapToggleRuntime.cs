using UnityEngine;
using UnityEngine.UI;

public class MainMinimapToggleRuntime : MonoBehaviour
{
    private GameObject miniMap;

    private void Start()
    {
        miniMap = FindSceneObject("MiniMap");
        if (miniMap != null)
            miniMap.SetActive(false);

        var buttonObject = FindSceneObject("MiniMapButton");
        if (buttonObject != null)
        {
            var button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
            button.onClick.RemoveListener(Toggle);
            button.onClick.AddListener(Toggle);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
            Toggle();
    }

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
