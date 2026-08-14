using UnityEngine;

namespace MysticJourney.API.Core
{
    /// <summary>
    /// Shared hidden host for API MonoBehaviours that need to run coroutines.
    /// Keeping them on one object prevents every endpoint from adding a root
    /// object to the runtime Hierarchy.
    /// </summary>
    internal static class ApiRuntimeHost
    {
        private const string HostName = "[ApiRuntime]";
        private static GameObject _host;

        public static GameObject GetOrCreate()
        {
            if (_host != null)
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

        public static T GetOrAdd<T>() where T : Component
        {
            var host = GetOrCreate();
            var component = host.GetComponent<T>();
            return component != null ? component : host.AddComponent<T>();
        }
    }
}
