using UnityEngine;

namespace MysticJourney.Core.Services
{
    // Executes mono behaviour operation.
    public class MapMusicPlayer : MonoBehaviour
    {
        [Tooltip("Nhạc nền của map này. Để trống = dừng nhạc khi vào map.")]
        [SerializeField] private AudioClip musicClip;

        [Tooltip("Nếu bật, sẽ phát lại từ đầu ngay cả khi đang phát đúng clip này.")]
        [SerializeField] private bool restartIfSame = false;

        // Refresh visible state and subscribe the event handlers required while this component is active.
        private void OnEnable()
        {
            AudioManager.Instance.PlayMusic(musicClip, restartIfSame);
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDisable()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopMusic();
            }
        }
    }
}
