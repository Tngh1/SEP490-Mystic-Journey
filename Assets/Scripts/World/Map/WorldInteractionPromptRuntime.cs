using UnityEngine;
using UnityEngine.UI;

public class WorldInteractionPromptRuntime : MonoBehaviour
{
    private static WorldInteractionPromptRuntime instance;
    private Text promptText;

    private static Font font;

    private static Font RuntimeFont
    {
        get
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }
    }

    public static void Show(string message)
    {
        var prompt = EnsureInstance();
        if (prompt == null)
            return;

        prompt.promptText.text = message ?? string.Empty;
        prompt.gameObject.SetActive(true);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.gameObject.SetActive(false);
    }

    private static WorldInteractionPromptRuntime EnsureInstance()
    {
        if (instance != null)
            return instance;

        var canvas = FindCanvas();
        if (canvas == null)
            return null;

        var go = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.18f);
        rect.anchorMax = new Vector2(0.5f, 0.18f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(280f, 58f);

        instance = go.AddComponent<WorldInteractionPromptRuntime>();
        instance.promptText = CreateText(go.transform);
        go.SetActive(false);
        return instance;
    }

    private static Text CreateText(Transform parent)
    {
        var textObject = new GameObject("PromptText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);

        var text = textObject.GetComponent<Text>();
        text.font = RuntimeFont;
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Canvas FindCanvas()
    {
        var canvasObject = FindMainSceneObject("Canvas");
        if (canvasObject != null && canvasObject.TryGetComponent(out Canvas canvas))
            return canvas;

        return FindFirstObjectByType<Canvas>();
    }

    private static GameObject FindMainSceneObject(string objectName)
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
