using UnityEngine;
using UnityEngine.UI;

namespace MysticJourney.Features.Dungeon.UI
{
    // Executes mono behaviour operation.
    public class UIDungeonPausePanel : MonoBehaviour
    {
        [Header("UI Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button exitButton;

        // Initializes internal component caches and dependencies for UIDungeonPausePanel upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitClicked);
            }
        }

        // Update visibility for panel; it updates active.
        public void OpenPanel()
        {
            gameObject.SetActive(true);

            Time.timeScale = 0f;
        }

        // Executes on resume clicked operation.
        private void OnResumeClicked()
        {
            Time.timeScale = 1f;

            gameObject.SetActive(false);
        }

        // Executes on exit clicked operation.
        private void OnExitClicked()
        {
            Time.timeScale = 1f;
            gameObject.SetActive(false);

            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.ReturnToWorldMap();
            }
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
