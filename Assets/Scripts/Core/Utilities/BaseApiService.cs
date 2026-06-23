using System;
using UnityEngine;

/// <summary>
/// Base class that all API endpoint singletons should inherit from.
/// Eliminates the repeated singleton boilerplate across all 13 API classes.
/// </summary>
public abstract class BaseApiService<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new();
    private static bool _applicationIsQuitting;

    public static T Instance
    {
        get
        {
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

                var go = new GameObject($"[{typeof(T).Name}]");
                PreserveAcrossScenes(go);
                _instance = go.AddComponent<T>();
                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = (T)(MonoBehaviour)this;
        PreserveAcrossScenes(gameObject);
    }

    private static void PreserveAcrossScenes(GameObject go)
    {
        if (go == null) return;

        if (go.transform.parent != null)
            go.transform.SetParent(null);

        DontDestroyOnLoad(go);
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    protected static void SafeDebugLog(string message)
    {
        Debug.Log($"[{typeof(T).Name}] {message}");
    }

    protected static void SafeDebugError(string message)
    {
        Debug.LogError($"[{typeof(T).Name}] {message}");
    }
}
