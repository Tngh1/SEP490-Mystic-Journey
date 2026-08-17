using UnityEngine;

namespace MysticJourney.API.Core
{
    // Initializes a new default instance of the ApiRuntimeHost class.
    internal static class ApiRuntimeHost
    {
        private const string HostName = "[ApiRuntime]";
        private static GameObject _host;

        // Executes get or create operation.
        public static GameObject GetOrCreate()
        {
            if (_host != null)  // Entity exists — proceed with conditional branch
                return _host;

            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate != null && candidate.name == HostName)
                {
                    _host = candidate;
                    _host.hideFlags = HideFlags.HideInHierarchy;
                    return _host;
                }
            }

            _host = new GameObject(HostName)
            {
                hideFlags = HideFlags.HideInHierarchy
            };

            if (Application.isPlaying)
                Object.DontDestroyOnLoad(_host);

            return _host;
        }

        // Executes component operation.
        public static T GetOrAdd<T>() where T : Component
        {
            var host = GetOrCreate();
            var component = host.GetComponent<T>();
            return component != null ? component : host.AddComponent<T>();
        }
    }
}
