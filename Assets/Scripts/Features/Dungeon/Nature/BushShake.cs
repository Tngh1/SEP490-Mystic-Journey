using UnityEngine;

// Executes mono behaviour operation.
public class BushShake3 : MonoBehaviour
{
    private bool m_IsShaking = false;
    private float m_ShakeTimer = 0f;
    private int m_ShakeStep = 0;
    private Quaternion m_StartRotation;

    private static readonly Quaternion SHAKE_RIGHT = Quaternion.Euler(0, 0, 8f);
    private static readonly Quaternion SHAKE_LEFT = Quaternion.Euler(0, 0, -8f);
    private static readonly Quaternion SHAKE_HALF = Quaternion.Euler(0, 0, 4f);

    private Transform m_Transform;

    // Initializes internal component caches and dependencies for BushShake3 upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        m_Transform = transform;
        this.enabled = false;
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (m_IsShaking) return;

        if (other.CompareTag("Player"))
        {
            m_IsShaking = true;
            m_StartRotation = m_Transform.localRotation;

            m_ShakeStep = 0;
            m_ShakeTimer = 0.06f;

            m_Transform.localRotation = m_StartRotation * SHAKE_RIGHT;

            this.enabled = true;
        }
    }

    // Per-frame update loop for BushShake3.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        m_ShakeTimer -= Time.deltaTime;
        if (m_ShakeTimer > 0f) return;

        m_ShakeStep++;
        m_ShakeTimer = 0.06f;

        if (m_ShakeStep == 1)
        {
            m_Transform.localRotation = m_StartRotation * SHAKE_LEFT;
        }
        else if (m_ShakeStep == 2)
        {
            m_Transform.localRotation = m_StartRotation * SHAKE_HALF;
        }
        else
        {
            m_Transform.localRotation = m_StartRotation;
            m_IsShaking = false;
            this.enabled = false;
        }
    }
}
