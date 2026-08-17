using System;
using MysticJourney.API.Core;
using UnityEngine;

// Executes core business logic for mono behaviour.
public abstract class BaseApiService<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new();
    private static bool _applicationIsQuitting;

#if UNITY_EDITOR
    // Initializes a new default instance of the BaseApiService class.
    static BaseApiService()
    {
        UnityEditor.EditorApplication.playModeStateChanged += state =>
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredPlayMode)
            {
                _applicationIsQuitting = false;
            }
        };
    }
#endif

    // Executes core business logic for instance.
    public static T Instance
    {
        get
        {
            if (!Application.isPlaying)
                return null;

            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[BaseApiService<{typeof(T).Name}>] Instance requested after application quit. Returning null.");
                return null;
            }

            lock (_lock)
            {
                if (_instance != null)
                    return _instance;

                var existing = FindObjectsByType<T>(FindObjectsSortMode.None);
                if (existing.Length > 0)
                {
                    _instance = existing[0];
                    return _instance;
                }

                _instance = ApiRuntimeHost.GetOrAdd<T>();
                return _instance;
            }
        }
    }

    // Initializes internal component caches and dependencies for BaseApiService upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = (T)(MonoBehaviour)this;
        PreserveAcrossScenes(gameObject);
    }

    // Executes core business logic for preserve across scenes.
    private static void PreserveAcrossScenes(GameObject go)
    {
        if (go == null) return;

        if (go.transform.parent != null)
            go.transform.SetParent(null);

        if (Application.isPlaying)
            DontDestroyOnLoad(go);
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // Executes core business logic for on application quit.
    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    // Executes core business logic for safe debug log.
    protected static void SafeDebugLog(string message)
    {
        Debug.Log($"[{typeof(T).Name}] {message}");
    }

    // Executes core business logic for safe debug error.
    protected static void SafeDebugError(string message)
    {
        Debug.LogError($"[{typeof(T).Name}] {message}");
    }
}
