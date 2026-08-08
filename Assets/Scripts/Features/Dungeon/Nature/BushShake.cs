using System.Collections;
using UnityEngine;

public class BushShake3 : MonoBehaviour
{
    private bool m_IsShaking = false;
    private Coroutine m_ShakeCoroutine;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (m_IsShaking) return;

        if (other.CompareTag("Player"))
        {
            if (m_ShakeCoroutine != null) StopCoroutine(m_ShakeCoroutine);
            m_ShakeCoroutine = StartCoroutine(ShakeRoutine());
        }
    }

    private IEnumerator ShakeRoutine()
    {
        m_IsShaking = true;
        Quaternion startRotation = transform.localRotation;

        // Rung nhẹ theo trục Z (2D tilt), không quay Y-axis (3D) làm hỏng batching của 2D Sprite
        transform.localRotation = startRotation * Quaternion.Euler(0, 0, 8f);
        yield return new WaitForSeconds(0.06f);

        transform.localRotation = startRotation * Quaternion.Euler(0, 0, -8f);
        yield return new WaitForSeconds(0.06f);

        transform.localRotation = startRotation * Quaternion.Euler(0, 0, 4f);
        yield return new WaitForSeconds(0.06f);

        transform.localRotation = startRotation;
        m_IsShaking = false;
        m_ShakeCoroutine = null;
    }
}
