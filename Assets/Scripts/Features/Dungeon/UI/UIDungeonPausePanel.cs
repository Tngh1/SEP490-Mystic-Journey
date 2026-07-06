using UnityEngine;
using UnityEngine.UI;

namespace MysticJourney.Features.Dungeon.UI
{
    public class UIDungeonPausePanel : MonoBehaviour
    {
        [Header("UI Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button exitButton;

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

        /// <summary>
        /// Gắn hàm này vào sự kiện OnClick của nút Pause nhỏ trên màn hình
        /// </summary>
        public void OpenPanel()
        {
            gameObject.SetActive(true);
            
            // Dừng thời gian trong game (quái vật, nhân vật sẽ đứng im)
            Time.timeScale = 0f;
        }

        private void OnResumeClicked()
        {
            // Trả lại thời gian chạy bình thường
            Time.timeScale = 1f;
            
            // Ẩn panel
            gameObject.SetActive(false);
        }

        private void OnExitClicked()
        {
            // Quan trọng: Phải trả lại thời gian bình thường trước khi chuyển cảnh, 
            // nếu không game sẽ bị đứng cứng ngắc ở Map mới
            Time.timeScale = 1f;
            gameObject.SetActive(false);
            
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.ReturnToWorldMap();
            }
        }

        private void OnDestroy()
        {
            // Phòng hờ trường hợp panel bị hủy khi đang pause
            Time.timeScale = 1f;
        }
    }
}
