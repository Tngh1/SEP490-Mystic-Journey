using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

namespace MysticJourney.Networking
{
    // Executes core business logic for i network scene manager.
    public sealed class PassThroughSceneManager : MonoBehaviour, INetworkSceneManager
    {

        // Executes core business logic for is busy.
        public bool IsBusy => false;

        // Executes core business logic for main runner scene.
        public Scene MainRunnerScene => SceneManager.GetActiveScene();

        // Executes core business logic for is runner scene.
        // Returns a boolean indicating operation success.
        public bool IsRunnerScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded;
        }

        // Executes core business logic for try get physics scene2 d.
        // Returns a boolean indicating operation success.
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

        // Executes core business logic for try get physics scene3 d.
        // Returns a boolean indicating operation success.
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

        // Executes core business logic for make dont destroy on load.
        public void MakeDontDestroyOnLoad(GameObject obj)
        {
            if (obj != null) Object.DontDestroyOnLoad(obj);
        }

        // Executes core business logic for move game object to scene.
        // Returns a boolean indicating operation success.
        public bool MoveGameObjectToScene(GameObject gameObject, SceneRef sceneRef)
        {
            if (gameObject == null) return false;
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

        // Executes core business logic for load scene.
        public NetworkSceneAsyncOp LoadScene(SceneRef sceneRef, NetworkLoadSceneParameters parameters)
        {
            return new NetworkSceneAsyncOp();
        }

        // Executes core business logic for unload scene.
        public NetworkSceneAsyncOp UnloadScene(SceneRef sceneRef)
        {
            return new NetworkSceneAsyncOp();
        }

        // Executes core business logic for get scene ref.
        // Logic details: validates required non-empty string arguments.
        public SceneRef GetSceneRef(GameObject gameObject)
        {
            if (gameObject == null) return SceneRef.None;
            return GetSceneRef(gameObject.scene.path);
        }

        // Executes core business logic for get scene ref.
        // Logic details: validates required non-empty string arguments.
        public SceneRef GetSceneRef(string sceneNameOrPath)
        {
            if (string.IsNullOrEmpty(sceneNameOrPath)) return SceneRef.None;
            int buildIndex = FusionUnitySceneManagerUtils.GetSceneBuildIndex(sceneNameOrPath);
            if (buildIndex >= 0) return SceneRef.FromIndex(buildIndex);
            return SceneRef.None;
        }

        // Executes core business logic for on scene info changed.
        // Returns a boolean indicating operation success.
        public bool OnSceneInfoChanged(NetworkSceneInfo sceneInfo, NetworkSceneInfoChangeSource changeSource)
        {
            return true;
        }

        // Executes core business logic for initialize.
        public void Initialize(NetworkRunner runner) { }

        // Executes core business logic for shutdown.
        public void Shutdown()
        {
        }


        // Handle scene load start using runner, scene ref, and load parameters and returns the computed result.
        public bool OnSceneLoadStart(NetworkRunner runner, SceneRef sceneRef,
                                     NetworkLoadSceneParameters loadParameters)
        {
            return true;
        }

        // Handle scene load start async using runner, scene ref, and load parameters and returns the computed result.
        public IEnumerator OnSceneLoadStartAsync(NetworkRunner runner, SceneRef sceneRef,
                                                NetworkLoadSceneParameters loadParameters)
        {
            yield break;
        }

        // Executes core business logic for on scene load end.
        public void OnSceneLoadEnd(NetworkRunner runner) { }

        // Executes core business logic for on scene unload start.
        public IEnumerator OnSceneUnloadStart(NetworkRunner runner, SceneRef sceneRef)
        {
            yield break;
        }

        // Executes core business logic for on scene unload end.
        public void OnSceneUnloadEnd(NetworkRunner runner) { }
    }
}
