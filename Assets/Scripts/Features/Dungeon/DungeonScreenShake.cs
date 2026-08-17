using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// Executes mono behaviour operation.
public class DungeonScreenShake : MonoBehaviour
{
    private static DungeonScreenShake _instance;
    // Executes instance operation.
    private static DungeonScreenShake Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[DungeonScreenShake]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<DungeonScreenShake>();
            }
            return _instance;
        }
    }

    // Initializes internal component caches and dependencies for DungeonScreenShake upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }


    // Executes shake operation.
    public static void Shake(float duration, float magnitude)
    {
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        Instance.StartCoroutine(Instance.ShakeRoutine(duration, magnitude));
    }


    // Executes shake routine operation.
    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[DungeonScreenShake] Camera.main not found. Shake skipped.");
            yield break;
        }

        CinemachineBrain brain = cam.GetComponent<CinemachineBrain>();
        if (brain != null) brain.enabled = false;

        Vector3 originLocalPos = cam.transform.localPosition;
        float   elapsed        = 0f;

        while (elapsed < duration)
        {
            float decay  = 1f - (elapsed / duration);
            float offset = magnitude * decay;

            cam.transform.localPosition = originLocalPos + new Vector3(
                // Randomize the eligible candidates before selecting this gameplay result.
                Random.Range(-offset, offset),
                // Randomize the eligible candidates before selecting this gameplay result.
                Random.Range(-offset, offset),
                0f
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.transform.localPosition = originLocalPos;
        if (brain != null) brain.enabled = true;

        Debug.Log("[DungeonScreenShake] Shake complete, CinemachineBrain restored.");
    }
}
