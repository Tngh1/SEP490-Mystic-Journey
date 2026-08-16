using UnityEngine;
using UnityEngine.UI;

public class MainMinimapToggleRuntime : MonoBehaviour
{
    private GameObject miniMap;

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

    // Map hotkey handling lives in PlayerUIHotkeys (single reader of the Map
    // action). This component only owns the on-screen minimap button toggle.

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

    private void OpenMapPanel()
    {
        // Trong dungeon thì không mở: panel chỉ dùng để dịch chuyển map, mà dịch chuyển
        // đang bị chặn. Im lặng bỏ qua, không báo gì.
        if (!MapUIManager.CanOpen) return;

        if (UIManager.Instance != null && UIManager.Instance.mapPanel != null)
        {
            UIManager.Instance.mapPanel.SetActive(true);
        }
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
