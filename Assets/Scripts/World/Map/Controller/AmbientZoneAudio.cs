using System.Collections;
using UnityEngine;
using MysticJourney.Core.Services;

// Executes mono behaviour operation.
public class AmbientZoneAudio : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Kéo file âm thanh (AudioClip) muốn phát lặp lại vào đây")]
    [SerializeField] private AudioClip audioClip;

    [Tooltip("Âm lượng âm thanh (từ 0.0 đến 1.0)")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Tooltip("Khoảng thời gian lặp lại phát âm thanh (tính bằng giây, mặc định 10s)")]
    [SerializeField] private float intervalSeconds = 10f;

    [Tooltip("Phát âm thanh ngay lập tức khi vừa bước vào vùng (sau đó mới đợi 10s lặp lại)")]
    [SerializeField] private bool playImmediatelyOnEnter = true;

    [Header("Detection Mode")]
    [Tooltip("Nếu true: Dùng khoảng cách bán kính (detectionRadius). Nếu false: Dùng Collider 2D (Is Trigger = true)")]
    [SerializeField] private bool useDistanceCheck = false;

    [Tooltip("Bán kính phát hiện người chơi khi bật useDistanceCheck")]
    [SerializeField] private float detectionRadius = 8f;

    private Coroutine _audioCoroutine;
    private bool _isPlayerInside = false;

    // Per-frame update loop for AmbientZoneAudio.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (useDistanceCheck)
        {
            CheckPlayerDistance();
        }
    }

    // Executes check player distance operation.
    private void CheckPlayerDistance()
    {
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        bool inRange = distance <= detectionRadius;

        if (inRange && !_isPlayerInside)
        {
            OnPlayerEnter();
        }
        else if (!inRange && _isPlayerInside)
        {
            OnPlayerExit();
        }
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (useDistanceCheck) return;

        if (collision.CompareTag("Player"))
        {
            OnPlayerEnter();
        }
    }

    // Executes on trigger exit2 d operation.
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (useDistanceCheck) return;

        if (collision.CompareTag("Player"))
        {
            OnPlayerExit();
        }
    }

    // Executes on player enter operation.
    private void OnPlayerEnter()
    {
        if (_isPlayerInside) return;
        _isPlayerInside = true;

        if (_audioCoroutine != null)
        {
            StopCoroutine(_audioCoroutine);
        }
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        _audioCoroutine = StartCoroutine(PlayRepeatingAudioRoutine());
    }

    // Executes on player exit operation.
    private void OnPlayerExit()
    {
        _isPlayerInside = false;

        if (_audioCoroutine != null)
        {
            StopCoroutine(_audioCoroutine);
            _audioCoroutine = null;
        }
    }

    // Executes play repeating audio routine operation.
    private IEnumerator PlayRepeatingAudioRoutine()
    {
        if (playImmediatelyOnEnter)
        {
            PlaySound();
            yield return new WaitForSeconds(intervalSeconds);
        }

        while (_isPlayerInside)
        {
            if (!playImmediatelyOnEnter)
            {
                yield return new WaitForSeconds(intervalSeconds);
                if (!_isPlayerInside) yield break;
                PlaySound();
            }
            else
            {
                PlaySound();
                yield return new WaitForSeconds(intervalSeconds);
            }
        }
    }

    // Executes play sound operation.
    private void PlaySound()
    {
        if (audioClip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(audioClip, soundVolume);
        }
    }

    // Executes get player transform operation.
    private Transform GetPlayerTransform()
    {
        if (PlayerEntity.Instance != null)
        {
            return PlayerEntity.Instance.transform;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            return playerObj.transform;
        }

        return null;
    }

    // Executes on draw gizmos selected operation.
    private void OnDrawGizmosSelected()
    {
        if (useDistanceCheck)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
