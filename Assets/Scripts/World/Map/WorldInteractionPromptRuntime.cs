using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Executes mono behaviour operation.
public class WorldInteractionPromptRuntime : MonoBehaviour
{
    private static WorldInteractionPromptRuntime instance;
    private Text promptText;
    private TMP_Text promptTMP;

    private static Font font;
    private string lastMessage;

    // Executes runtime font operation.
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

    // Initializes internal component caches and dependencies for WorldInteractionPromptRuntime upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
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

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // Executes show operation.
    public static void Show(string message)
    {
        var prompt = EnsureInstance();
        if (prompt == null)
            return;

        string targetMsg = message ?? string.Empty;

        if (prompt.lastMessage == targetMsg && prompt.gameObject.activeSelf)
            return;

        prompt.lastMessage = targetMsg;

        if (prompt.promptTMP != null)
            prompt.promptTMP.text = targetMsg;
        else if (prompt.promptText != null)
            prompt.promptText.text = targetMsg;

        if (!prompt.gameObject.activeSelf)
            prompt.gameObject.SetActive(true);
    }

    // Executes hide operation.
    public static void Hide()
    {
        if (instance != null)
        {
            instance.lastMessage = null;
            if (instance.gameObject.activeSelf)
                instance.gameObject.SetActive(false);
        }
    }

    // Executes ensure instance operation.
    private static WorldInteractionPromptRuntime EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = Object.FindFirstObjectByType<WorldInteractionPromptRuntime>(FindObjectsInactive.Include);
        if (instance != null)
        {
            if (instance.promptText == null) instance.promptText = instance.GetComponentInChildren<Text>(true);
            if (instance.promptTMP == null) instance.promptTMP = instance.GetComponentInChildren<TMP_Text>(true);
            return instance;
        }

        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && (c.name == "Canvas" || c.name == "HUD" || c.name == "MainCanvas"))
            {
                canvas = c;
                break;
            }
        }
        if (canvas == null)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas = c;
                    break;
                }
            }
        }
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
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

    // Executes create text operation.
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
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.95f, 0.8f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);

        return text;
    }
}
