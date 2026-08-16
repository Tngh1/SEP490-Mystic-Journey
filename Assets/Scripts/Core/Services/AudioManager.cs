using System.Collections.Generic;
using UnityEngine;

namespace MysticJourney.Core.Services
{
    /// <summary>
    /// Trung tâm quản lý âm thanh: nhạc nền (BGM) + hiệu ứng (SFX).
    /// Singleton bền vững qua các scene (DontDestroyOnLoad), tự tạo khi truy cập lần đầu.
    ///
    /// Âm lượng thực tế = Master * (Music|SFX), và bị chặn về 0 khi IsMuted.
    /// Mọi âm thanh nên đi qua đây để slider trong Game Setting điều khiển được:
    ///   - Nhạc nền:  AudioManager.Instance.PlayMusic(clip)
    ///   - Hiệu ứng:  AudioManager.Instance.PlaySfx(clip)
    ///
    /// Khi thay đổi volume trong Settings, gọi AudioManager.Instance.ApplyVolumesFromSettings().
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        private static bool _isQuitting = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _instance = null;
            _isQuitting = false;
        }

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying && !_isQuitting)
                {
                    var go = new GameObject("[AudioManager]");
                    _instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("UI SFX")]
        [Tooltip("Tiếng phát khi mở panel UI. Kéo clip OpenPanel vào đây.")]
        [SerializeField] private AudioClip openPanelSfx;

        // Nguồn phát nhạc nền (loop). Một bài tại một thời điểm.
        private AudioSource _musicSource;
        // Nguồn phát SFX (one-shot). Dùng PlayOneShot nên 1 source là đủ cho phần lớn nhu cầu.
        private AudioSource _sfxSource;

        // Clip nhạc đang phát — tránh phát lại chính nó khi đổi map trùng nhạc.
        private AudioClip _currentMusicClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            EnsureSources();

            // Áp volume đã lưu ngay khi khởi tạo.
            SettingsService.Instance.Load();
            ApplyVolumesFromSettings();
        }

        private void EnsureSources()
        {
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.playOnAwake = false;
                _musicSource.loop = true;
                _musicSource.spatialBlend = 0f; // 2D
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.spatialBlend = 0f; // 2D
            }
        }

        // ─── Nhạc nền ─────────────────────────────────────────────────────────

        /// <summary>Phát nhạc nền. Nếu clip đang phát trùng thì bỏ qua (không restart).</summary>
        public void PlayMusic(AudioClip clip, bool restartIfSame = false)
        {
            EnsureSources();

            if (clip == null)
            {
                StopMusic();
                return;
            }

            if (!restartIfSame && _currentMusicClip == clip && _musicSource.isPlaying)
                return;

            _currentMusicClip = clip;
            _musicSource.clip = clip;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource != null) _musicSource.Stop();
            _currentMusicClip = null;
        }

        // ─── Hiệu ứng (SFX) ───────────────────────────────────────────────────

        /// <summary>Phát 1 hiệu ứng. volumeScale cho phép chỉnh nhỏ/to riêng từng clip.</summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            EnsureSources();
            // PlayOneShot nhân với _sfxSource.volume (đã set theo Master*SFX).
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        /// <summary>Tiếng mở panel UI. Đi qua kênh SFX nên slider SFX điều khiển được.</summary>
        public void PlayOpenPanel() => PlaySfx(openPanelSfx);

        // ─── Âm lượng ─────────────────────────────────────────────────────────

        /// <summary>
        /// Đọc giá trị từ SettingsService và áp vào 2 source.
        /// Gọi mỗi khi người dùng kéo slider hoặc bật/tắt mute.
        /// </summary>
        public void ApplyVolumesFromSettings()
        {
            EnsureSources();

            var s = SettingsService.Instance;
            float master = s.IsMuted ? 0f : s.MasterVolume;

            _musicSource.volume = master * s.MusicVolume;
            _sfxSource.volume = master * s.SfxVolume;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _isQuitting = true;
            }
        }
    }
}
