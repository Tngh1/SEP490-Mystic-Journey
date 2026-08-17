using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class UISimpleTooltip : MonoBehaviour
{
    // Executes instance operation.
    public static UISimpleTooltip Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI textComponent;
    [SerializeField] private RectTransform backgroundRect;

    // Initializes internal component caches and dependencies for UISimpleTooltip upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);

        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private string lastTitle;
    private string lastTimeText;

    // Executes show operation.
    public void Show(string title, string timeText, Vector2 position)
    {
        gameObject.SetActive(true);
        transform.position = position;

        if (lastTitle != title || lastTimeText != timeText)
        {
            textComponent.text = title + "\n" + timeText;
            lastTimeText = timeText;
        }

        if (lastTitle != title)
        {
            lastTitle = title;
            if (backgroundRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);
            }
        }
    }

    // Update visibility for the current state; it updates active.
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
