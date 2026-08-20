using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MysticJourney.UI.Responsive
{
    /// <summary>
    /// Keeps every root screen-space Canvas responsive, including canvases created at runtime.
    /// World-space UI is intentionally left unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenCompatibilityManager : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        private const float CanvasScanInterval = 1f;
        private static ScreenCompatibilityManager instance;

        private int lastScreenWidth;
        private int lastScreenHeight;
        private float nextCanvasScanTime;
        private Coroutine sceneRefreshRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
                return;

            var host = new GameObject(nameof(ScreenCompatibilityManager));
            instance = host.AddComponent<ScreenCompatibilityManager>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            RememberScreenSize();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToLoadedCanvases();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            bool sizeChanged = lastScreenWidth != UnityEngine.Screen.width ||
                               lastScreenHeight != UnityEngine.Screen.height;
            if (!sizeChanged && Time.unscaledTime < nextCanvasScanTime)
                return;

            RememberScreenSize();
            nextCanvasScanTime = Time.unscaledTime + CanvasScanInterval;
            ApplyToLoadedCanvases();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (sceneRefreshRoutine != null)
                StopCoroutine(sceneRefreshRoutine);

            sceneRefreshRoutine = StartCoroutine(ApplyAfterSceneLayout());
        }

        private IEnumerator ApplyAfterSceneLayout()
        {
            yield return null;
            ApplyToLoadedCanvases();
            sceneRefreshRoutine = null;
        }

        private void RememberScreenSize()
        {
            lastScreenWidth = UnityEngine.Screen.width;
            lastScreenHeight = UnityEngine.Screen.height;
        }

        public static void ApplyToLoadedCanvases()
        {
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null ||
                    !canvas.gameObject.scene.IsValid() ||
                    !canvas.isRootCanvas ||
                    canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                    scaler = canvas.gameObject.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
            }
        }
    }
}
