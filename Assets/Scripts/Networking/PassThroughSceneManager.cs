using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

namespace MysticJourney.Networking
{
    /// <summary>
    /// No-op <see cref="INetworkSceneManager"/>. Networking works in the currently loaded scene(s)
    /// without unloading or reloading them. This keeps gameplay UI / systems in <c>Main</c> intact
    /// when Photon connects.
    /// </summary>
    public sealed class PassThroughSceneManager : MonoBehaviour, INetworkSceneManager
    {
        // ─────────────────────────────────────────────────────────────────────────
        // INetworkSceneManager
        // ─────────────────────────────────────────────────────────────────────────

        public bool IsBusy => false;

        // ponytail: this is the ACTIVE scene (Main, the UI scene), while the runner is
        // started with the SceneRef of the map scene, so TryGetPhysicsScene2D below hands
        // Fusion a physics scene that holds no gameplay colliders. Harmless today because
        // lag compensation is off; if it is ever enabled, resolve the map scene
        // (WorldState.CurrentMapName) here instead of the active one.
        public Scene MainRunnerScene => SceneManager.GetActiveScene();

        public bool IsRunnerScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded;
        }

        public bool TryGetPhysicsScene2D(out PhysicsScene2D scene2D)
        {
            var main = MainRunnerScene;
            if (main.IsValid())
            {
                scene2D = main.GetPhysicsScene2D();
                return true;
            }
            scene2D = default;
            return false;
        }

        public bool TryGetPhysicsScene3D(out PhysicsScene scene3D)
        {
            var main = MainRunnerScene;
            if (main.IsValid())
            {
                scene3D = main.GetPhysicsScene();
                return true;
            }
            scene3D = default;
            return false;
        }

        public void MakeDontDestroyOnLoad(GameObject obj)
        {
            if (obj != null) Object.DontDestroyOnLoad(obj);
        }

        public bool MoveGameObjectToScene(GameObject gameObject, SceneRef sceneRef)
        {
            if (gameObject == null) return false;
            // Find first loaded scene that matches the requested SceneRef, otherwise fall back
            // to the active scene. We deliberately do NOT call SceneManager.MoveGameObjectToScene
            // for the active scene because Unity disallows moving objects into the active scene
            // when the caller is in that same scene.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.IsValid() || !s.isLoaded) continue;
                if (GetSceneRef(s.path) == sceneRef)
                {
                    if (s != gameObject.scene)
                        SceneManager.MoveGameObjectToScene(gameObject, s);
                    return true;
                }
            }
            return false;
        }

        public NetworkSceneAsyncOp LoadScene(SceneRef sceneRef, NetworkLoadSceneParameters parameters)
        {
            // No-op: the gameplay scene is already loaded by GameBootstrap. Returning an empty
            // NetworkSceneAsyncOp signals to Fusion that the request was satisfied immediately.
            return new NetworkSceneAsyncOp();
        }

        public NetworkSceneAsyncOp UnloadScene(SceneRef sceneRef)
        {
            return new NetworkSceneAsyncOp();
        }

        public SceneRef GetSceneRef(GameObject gameObject)
        {
            if (gameObject == null) return SceneRef.None;
            return GetSceneRef(gameObject.scene.path);
        }

        public SceneRef GetSceneRef(string sceneNameOrPath)
        {
            if (string.IsNullOrEmpty(sceneNameOrPath)) return SceneRef.None;
            int buildIndex = FusionUnitySceneManagerUtils.GetSceneBuildIndex(sceneNameOrPath);
            if (buildIndex >= 0) return SceneRef.FromIndex(buildIndex);
            return SceneRef.None;
        }

        public bool OnSceneInfoChanged(NetworkSceneInfo sceneInfo, NetworkSceneInfoChangeSource changeSource)
        {
            // Handled at the runner level — we never unload so there's nothing to change.
            return true;
        }

        public void Initialize(NetworkRunner runner) { }

        public void Shutdown()
        {
            // No-op
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Legacy/optional callbacks (kept as no-ops for older Fusion builds).
        // ─────────────────────────────────────────────────────────────────────────

        public bool OnSceneLoadStart(NetworkRunner runner, SceneRef sceneRef,
                                     NetworkLoadSceneParameters loadParameters)
        {
            return true;
        }

        public IEnumerator OnSceneLoadStartAsync(NetworkRunner runner, SceneRef sceneRef,
                                                NetworkLoadSceneParameters loadParameters)
        {
            yield break;
        }

        public void OnSceneLoadEnd(NetworkRunner runner) { }

        public IEnumerator OnSceneUnloadStart(NetworkRunner runner, SceneRef sceneRef)
        {
            yield break;
        }

        public void OnSceneUnloadEnd(NetworkRunner runner) { }
    }
}