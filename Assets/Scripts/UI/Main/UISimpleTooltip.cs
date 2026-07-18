using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UISimpleTooltip : MonoBehaviour
{
    public static UISimpleTooltip Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI textComponent;
    [SerializeField] private RectTransform backgroundRect;
    
    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
        
        // Prevent tooltip from blocking mouse raycasts (which causes rapid flicker)
        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private string lastTitle;
    private string lastTimeText;

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

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
