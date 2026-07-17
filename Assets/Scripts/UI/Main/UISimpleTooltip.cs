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
    }

    public void Show(string content, Vector2 position)
    {
        gameObject.SetActive(true);
        textComponent.text = content;
        
        if (backgroundRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);
        }

        transform.position = position;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
