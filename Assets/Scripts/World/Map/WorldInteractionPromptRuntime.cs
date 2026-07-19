using UnityEngine;
using UnityEngine.UI;

using TMPro;

public class WorldInteractionPromptRuntime : MonoBehaviour
{
    private static WorldInteractionPromptRuntime instance;
    private Text promptText;
    private TMP_Text promptTMP;

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

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            promptText = GetComponentInChildren<Text>(true);
            promptTMP = GetComponentInChildren<TMP_Text>(true);
            gameObject.SetActive(false);
        }
    }

    public static void Show(string message)
    {
        var prompt = EnsureInstance();
        if (prompt == null)
            return;

        if (prompt.promptTMP != null)
            prompt.promptTMP.text = message ?? string.Empty;
        else if (prompt.promptText != null)
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

        // Try to find an existing InteractionPrompt in the scene (e.g. from the user's Prefab)
        var existing = FindMainSceneObject("InteractionPrompt");
        if (existing != null)
        {
            instance = existing.GetComponent<WorldInteractionPromptRuntime>();
            if (instance == null)
                instance = existing.AddComponent<WorldInteractionPromptRuntime>();
            
            instance.promptText = existing.GetComponentInChildren<Text>(true);
            instance.promptTMP = existing.GetComponentInChildren<TMP_Text>(true);
            return instance;
        }

        var canvas = FindCanvas();
        if (canvas == null)
            return null;

        var go = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.15f);
        rect.anchorMax = new Vector2(0.5f, 0.15f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(240f, 65f);

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
        text.fontSize = 18; // Tăng cỡ chữ lên xíu
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.95f, 0.8f); // Trắng hơi ngả vàng cho hợp style RPG
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // Thêm viền đen cho chữ dễ đọc
        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);
        
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
