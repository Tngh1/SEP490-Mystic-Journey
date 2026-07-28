using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MysticJourney.Features.Quest
{
    public class QuestVideoManager : MonoBehaviour
    {
        public static QuestVideoManager Instance { get; private set; }

        public static bool IsVideoPlaying { get; private set; }

        private GameObject _videoCanvasOverlay;
        private Canvas _overlayCanvas;
        private RawImage _videoRawImage;
        private RenderTexture _renderTexture;
        private readonly List<GameObject> _hiddenUiElements = new List<GameObject>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void NotifyVideoStarted(VideoPlayer vp = null)
        {
            EnsureInstance();
            IsVideoPlaying = true;

            if (Instance != null)
            {
                Instance.HideAllMainUi();
                Instance.SetupFullScreenVideoOverlay(vp);
            }

            Debug.Log("[QuestVideoManager] Video started playing. Main UI hidden, completion popups paused.");
        }

        public static void NotifyVideoEnded(VideoPlayer vp = null)
        {
            IsVideoPlaying = false;

            if (Instance != null)
            {
                Instance.CleanupVideoOverlay(vp);
                Instance.RestoreMainUi();
            }

            Debug.Log("[QuestVideoManager] Video ended. Main UI restored, completion popups resumed.");
        }

        private static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("QuestVideoManager");
            Instance = go.AddComponent<QuestVideoManager>();
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }


        private void HideAllMainUi()
        {
            _hiddenUiElements.Clear();

            // 1. Target key UI containers in scene
            var targetNames = new[] { "HUD", "QuestTracker", "QuestPanel", "NPCPanel", "PlayerHUD", "Minimap", "Canvas", "MainCanvas" };
            var allSceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (var go in allSceneObjects)
            {
                if (go == null || !go.scene.IsValid() || !go.activeSelf) continue;
                if (go.name == "QuestVideoManager" || go.name.Contains("VideoOverlay")) continue;

                for (int i = 0; i < targetNames.Length; i++)
                {
                    if (go.name.Equals(targetNames[i], System.StringComparison.OrdinalIgnoreCase))
                    {
                        go.SetActive(false);
                        _hiddenUiElements.Add(go);
                        break;
                    }
                }
            }

            // 2. Hide WorldInteractionPromptRuntime
            WorldInteractionPromptRuntime.Hide();
        }

        private void RestoreMainUi()
        {
            foreach (var go in _hiddenUiElements)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }
            _hiddenUiElements.Clear();
            WorldRuntimeEvents.RaiseQuestsChanged();
        }


        private void SetupFullScreenVideoOverlay(VideoPlayer vp)
        {
            if (vp == null) return;

            // Enforce max sorting order on Canvas if VideoPlayer is on a UI Canvas
            var parentCanvas = vp.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentCanvas.overrideSorting = true;
                parentCanvas.sortingOrder = 32767;
                return;
            }

            // Create full-screen black canvas overlay with highest sorting order
            if (_videoCanvasOverlay == null)
            {
                _videoCanvasOverlay = new GameObject("QuestVideoCanvasOverlay");
                if (Application.isPlaying) DontDestroyOnLoad(_videoCanvasOverlay);


                _overlayCanvas = _videoCanvasOverlay.AddComponent<Canvas>();
                _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _overlayCanvas.overrideSorting = true;
                _overlayCanvas.sortingOrder = 32767; // Maximum sorting order to cover ALL UI

                var scaler = _videoCanvasOverlay.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                var panel = new GameObject("BlackBackground");
                panel.transform.SetParent(_videoCanvasOverlay.transform, false);
                var bgImage = panel.AddComponent<Image>();
                bgImage.color = Color.black;
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.one;

                var rawGo = new GameObject("VideoRawImage");
                rawGo.transform.SetParent(_videoCanvasOverlay.transform, false);
                _videoRawImage = rawGo.AddComponent<RawImage>();
                var rawRect = rawGo.GetComponent<RectTransform>();
                rawRect.anchorMin = Vector2.zero;
                rawRect.anchorMax = Vector2.one;
                rawRect.offsetMin = Vector2.zero;
                rawRect.offsetMax = Vector2.one;
            }

            _videoCanvasOverlay.SetActive(true);

            // Configure VideoPlayer to render into RenderTexture for the RawImage
            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(1920, 1080, 16);
            }
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = _renderTexture;
            _videoRawImage.texture = _renderTexture;
            _videoRawImage.color = Color.white;
        }

        private void CleanupVideoOverlay(VideoPlayer vp)
        {
            if (_videoCanvasOverlay != null)
            {
                _videoCanvasOverlay.SetActive(false);
            }
            if (vp != null && vp.targetTexture == _renderTexture)
            {
                vp.targetTexture = null;
            }
        }
    }
}
