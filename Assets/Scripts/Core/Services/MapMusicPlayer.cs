using UnityEngine;

namespace MysticJourney.Core.Services
{
    /// <summary>
    /// Gắn component này vào một GameObject trong mỗi scene map, kéo AudioClip nhạc nền
    /// vào field <see cref="musicClip"/>. Khi scene bật lên, nó yêu cầu AudioManager phát
    /// nhạc đó (đi qua kênh Music nên slider Music trong Game Setting điều khiển được).
    ///
    /// Cách thêm nhạc cho map mới về sau:
    ///   1. Tạo 1 GameObject rỗng trong scene (vd "BGM").
    ///   2. Add component MapMusicPlayer.
    ///   3. Kéo AudioClip nhạc nền vào field Music Clip.
    /// KHÔNG cần AudioSource trong scene nữa — AudioManager sở hữu source dùng chung.
    /// </summary>
    public class MapMusicPlayer : MonoBehaviour
    {
        [Tooltip("Nhạc nền của map này. Để trống = dừng nhạc khi vào map.")]
        [SerializeField] private AudioClip musicClip;

        [Tooltip("Nếu bật, sẽ phát lại từ đầu ngay cả khi đang phát đúng clip này.")]
        [SerializeField] private bool restartIfSame = false;

        private void OnEnable()
        {
            // OnEnable thay vì Start để nhạc đổi đúng khi scene được kích hoạt lại
            // (các world scene được load/active động qua GameBootstrap).
            AudioManager.Instance.PlayMusic(musicClip, restartIfSame);
        }
    }
}
