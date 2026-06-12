using UnityEngine;

public class PersistentSceneLoader : MonoBehaviour
{
    private static bool loaded;

    private void Awake()
    {
        if (loaded)
            return;

        loaded = true;

        DontDestroyOnLoad(gameObject);
    }
}