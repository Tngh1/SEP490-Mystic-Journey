using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Friend
{
    public class UIBuffSlot : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text statText;

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (iconImage == null)
                iconImage = transform.Find("Icon")?.GetComponent<Image>() ?? GetComponentInChildren<Image>();

            if (statText == null)
                statText = transform.Find("StatText")?.GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>();
        }

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
