using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Lightweight screen shake for the dungeon scene.
/// Temporarily disables the CinemachineBrain so the direct camera offset is not overwritten.
///
/// Usage:
///   DungeonScreenShake.Shake(duration: 0.9f, magnitude: 0.28f);
///
/// The singleton is created on demand and persists across scenes.
/// </summary>
public class DungeonScreenShake : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    private static DungeonScreenShake _instance;
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

    // ═══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Triggers a screen shake effect.
    /// Temporarily disables the CinemachineBrain (if present) so the camera
    /// offset is not overwritten by Cinemachine during the shake.
    /// </summary>
    /// <param name="duration">Total shake duration in seconds (e.g. 0.8f).</param>
    /// <param name="magnitude">Peak offset in world units (e.g. 0.25f).</param>
    public static void Shake(float duration, float magnitude)
    {
        Instance.StartCoroutine(Instance.ShakeRoutine(duration, magnitude));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════════════════════

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[DungeonScreenShake] Camera.main not found. Shake skipped.");
            yield break;
        }

        // Disable CinemachineBrain so our offset isn't overwritten every LateUpdate
        CinemachineBrain brain = cam.GetComponent<CinemachineBrain>();
        if (brain != null) brain.enabled = false;

        Vector3 originLocalPos = cam.transform.localPosition;
        float   elapsed        = 0f;

        while (elapsed < duration)
        {
            // Gradually reduce magnitude toward end for a smooth settle
            float decay  = 1f - (elapsed / duration);
            float offset = magnitude * decay;

            cam.transform.localPosition = originLocalPos + new Vector3(
                Random.Range(-offset, offset),
                Random.Range(-offset, offset),
                0f
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Restore exact original position and re-enable Cinemachine
        cam.transform.localPosition = originLocalPos;
        if (brain != null) brain.enabled = true;

        Debug.Log("[DungeonScreenShake] Shake complete, CinemachineBrain restored.");
    }
}
