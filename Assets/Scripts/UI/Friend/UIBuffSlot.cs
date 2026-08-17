using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Friend
{
    // Executes mono behaviour operation.
    public class UIBuffSlot : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text statText;

        // Initializes internal component caches and dependencies for UIBuffSlot upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            EnsureReferences();
        }

        // Executes ensure references operation.
        private void EnsureReferences()
        {
            if (iconImage == null)
                iconImage = transform.Find("Icon")?.GetComponent<Image>() ?? GetComponentInChildren<Image>();

            if (statText == null)
                statText = transform.Find("StatText")?.GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>();
        }

        // Executes setup operation.
        public void Setup(string text, Sprite icon)
        {
            EnsureReferences();

            if (statText != null)
                statText.text = text ?? string.Empty;

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                if (icon != null)
                    iconImage.preserveAspect = true;
            }
        }
    }
}
