using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace UI.Combat
{
    // Executes i pointer exit handler operation.
    public class BuffIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image buffImage;
        [SerializeField] private TMP_Text durationText;
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TMP_Text tooltipNameText;
        [SerializeField] private TMP_Text tooltipDurationText;

        private string _buffName;
        private float _duration;
        private bool _isDebuff;

        // Executes setup operation.
        public void Setup(string buffName, Sprite icon, float duration, bool isDebuff)
        {
            _buffName = buffName;
            _duration = duration;
            _isDebuff = isDebuff;

            if (buffImage != null)
                buffImage.sprite = icon;

            if (tooltipNameText != null)
                tooltipNameText.text = _isDebuff ? $"<color=red>[Debuff] {_buffName}</color>" : $"<color=green>[Buff] {_buffName}</color>";

            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);

            UpdateDurationUI();
        }

        // Per-frame update loop for BuffIconUI.
        // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
        private void Update()
        {
            if (_duration > 0)
            {
                _duration -= Time.deltaTime;
                UpdateDurationUI();

                if (_duration <= 0)
                {
                    Destroy(gameObject);
                }
            }
        }

        // Executes update duration ui operation.
        private void UpdateDurationUI()
        {
            string timeStr = _duration > 60 ? $"{Mathf.CeilToInt(_duration / 60)}m" : $"{Mathf.CeilToInt(_duration)}s";

            if (durationText != null)
                durationText.text = timeStr;

            if (tooltipDurationText != null)
                tooltipDurationText.text = $"Duration: {timeStr}";
        }

        // Executes on pointer enter operation.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(true);
        }

        // Executes on pointer exit operation.
        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }
    }
}
