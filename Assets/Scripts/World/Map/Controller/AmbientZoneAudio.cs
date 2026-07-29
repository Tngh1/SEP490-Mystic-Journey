using System.Collections;
using UnityEngine;
using MysticJourney.Core.Services;

/// <summary>
/// Phát một đoạn âm thanh lặp đi lặp lại (mặc định 10s/lần) khi người chơi bước vào khu vực điểm khám phá.
/// Tự động dừng phát khi người chơi đi ra khỏi khu vực.
/// Hỗ trợ cả 2 cách phát hiện: Dùng Collider 2D (isTrigger = true) HOẶC Bán kính khoảng cách (Distance Check).
/// </summary>
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

    private void Update()
    {
        if (useDistanceCheck)
        {
            CheckPlayerDistance();
        }
    }

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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (useDistanceCheck) return;

        if (collision.CompareTag("Player"))
        {
            OnPlayerEnter();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (useDistanceCheck) return;

        if (collision.CompareTag("Player"))
        {
            OnPlayerExit();
        }
    }

    private void OnPlayerEnter()
    {
        if (_isPlayerInside) return;
        _isPlayerInside = true;

        if (_audioCoroutine != null)
        {
            StopCoroutine(_audioCoroutine);
        }
        _audioCoroutine = StartCoroutine(PlayRepeatingAudioRoutine());
    }

    private void OnPlayerExit()
    {
        _isPlayerInside = false;

        if (_audioCoroutine != null)
        {
            StopCoroutine(_audioCoroutine);
            _audioCoroutine = null;
        }
    }

    private IEnumerator PlayRepeatingAudioRoutine()
    {
        // Lần phát đầu tiên khi bước vào khu vực
        if (playImmediatelyOnEnter)
        {
            PlaySound();
            yield return new WaitForSeconds(intervalSeconds);
        }

        // Lặp lại định kỳ mỗi intervalSeconds (10 giây) chừng nào người chơi còn ở trong vùng
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

    private void PlaySound()
    {
        if (audioClip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(audioClip, soundVolume);
        }
    }

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

    private void OnDrawGizmosSelected()
    {
        if (useDistanceCheck)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
