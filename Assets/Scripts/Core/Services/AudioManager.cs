using System.Collections.Generic;
using UnityEngine;

namespace MysticJourney.Core.Services
{
    // Executes core business logic for mono behaviour.
    [DefaultExecutionOrder(-50)]
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        private static bool _isQuitting = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        // Executes core business logic for init.
        private static void Init()
        {
            _instance = null;
            _isQuitting = false;
        }

        // Executes core business logic for instance.
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

        private AudioSource _musicSource;
        private AudioSource _sfxSource;

        private AudioClip _currentMusicClip;

        // Initializes internal component caches and dependencies for AudioManager upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
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

            SettingsService.Instance.Load();
            ApplyVolumesFromSettings();
        }

        // Executes core business logic for ensure sources.
        private void EnsureSources()
        {
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.playOnAwake = false;
                _musicSource.loop = true;
                _musicSource.spatialBlend = 0f;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.spatialBlend = 0f;
            }
        }


        // Executes core business logic for play music.
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

        // Executes core business logic for stop music.
        public void StopMusic()
        {
            if (_musicSource != null) _musicSource.Stop();
            _currentMusicClip = null;
        }


        // Executes core business logic for play sfx.
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            EnsureSources();
            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        // Executes core business logic for play open panel.
        public void PlayOpenPanel() => PlaySfx(openPanelSfx);


        // Executes core business logic for apply volumes from settings.
        public void ApplyVolumesFromSettings()
        {
            EnsureSources();

            var s = SettingsService.Instance;
            float master = s.IsMuted ? 0f : s.MasterVolume;

            _musicSource.volume = master * s.MusicVolume;
            _sfxSource.volume = master * s.SfxVolume;
        }

        // Executes core business logic for on application quit.
        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _isQuitting = true;
            }
        }
    }
}
