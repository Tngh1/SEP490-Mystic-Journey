using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isPlaying || _instance != null || _isQuitting)
                return;

            var go = new GameObject("[AudioManager]");
            _instance = go.AddComponent<AudioManager>();
            DontDestroyOnLoad(go);
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
        [Tooltip("Tiếng phát khi click button UI. Kéo Assets/UI/Audio/Button/Button.mp3 vào đây.")]
        [SerializeField] private AudioClip buttonClickSfx;
        [Tooltip("Tiếng bước chân. Kéo Assets/UI/Audio/Player/Walking.mp3 vào đây.")]
        [SerializeField] private AudioClip walkingSfx;
        [Tooltip("Tiếng nhặt vật phẩm. Kéo Assets/UI/Audio/Player/Pickup.mp3 vào đây.")]
        [SerializeField] private AudioClip pickupSfx;

        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _walkingSource;

        private AudioClip _currentMusicClip;
        private readonly HashSet<Button> _registeredButtons = new HashSet<Button>();
        private float _nextButtonScanTime;

        // Initializes internal component caches and dependencies for AudioManager upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (_instance.buttonClickSfx == null) _instance.buttonClickSfx = buttonClickSfx;
                if (_instance.walkingSfx == null) _instance.walkingSfx = walkingSfx;
                if (_instance.pickupSfx == null) _instance.pickupSfx = pickupSfx;
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            EnsureSources();
            if (buttonClickSfx == null)
                buttonClickSfx = Resources.Load<AudioClip>("Audio/Button");
            if (walkingSfx == null)
                walkingSfx = Resources.Load<AudioClip>("Audio/Player/Walking");
            if (pickupSfx == null)
                pickupSfx = Resources.Load<AudioClip>("Audio/Player/Pickup");

            SceneManager.sceneLoaded += OnSceneLoaded;
            RegisterButtons();

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

            if (_walkingSource == null)
            {
                _walkingSource = gameObject.AddComponent<AudioSource>();
                _walkingSource.playOnAwake = false;
                _walkingSource.loop = true;
                _walkingSource.spatialBlend = 0f;
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

        public void PlayWalking()
        {
            if (walkingSfx == null) return;
            EnsureSources();
            if (_walkingSource.clip != walkingSfx)
                _walkingSource.clip = walkingSfx;
            if (!_walkingSource.isPlaying)
                _walkingSource.Play();
        }

        public void StopWalking()
        {
            if (_walkingSource != null)
                _walkingSource.Stop();
        }

        public void PlayPickup(float volumeScale = 1f) => PlaySfx(pickupSfx, volumeScale);

        // Executes core business logic for apply volumes from settings.
        public void ApplyVolumesFromSettings()
        {
            EnsureSources();

            var s = SettingsService.Instance;
            float master = s.IsMuted ? 0f : s.MasterVolume;

            _musicSource.volume = master * s.MusicVolume;
            _sfxSource.volume = master * s.SfxVolume;
            _walkingSource.volume = master * s.SfxVolume * 0.5f;
        }

        // Executes core business logic for on application quit.
        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextButtonScanTime)
                return;

            _nextButtonScanTime = Time.unscaledTime + 0.5f;
            RegisterButtons();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RegisterButtons();
        }

        private void RegisterButtons()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (button == null || !button.gameObject.scene.IsValid() || _registeredButtons.Contains(button))
                    continue;

                button.onClick.AddListener(PlayButtonClick);
                _registeredButtons.Add(button);
            }

            _registeredButtons.RemoveWhere(button => button == null);
        }

        private void PlayButtonClick()
        {
            PlaySfx(buttonClickSfx);
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (var button in _registeredButtons)
            {
                if (button != null)
                    button.onClick.RemoveListener(PlayButtonClick);
            }
            _registeredButtons.Clear();

            if (_instance == this)
            {
                _isQuitting = true;
            }
        }
    }
}
