using UnityEngine;

// Executes mono behaviour operation.
public class PersistentSceneLoader : MonoBehaviour
{
    private static bool loaded;

    // Initializes internal component caches and dependencies for PersistentSceneLoader upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (loaded)
            return;

        loaded = true;

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);
    }
}
