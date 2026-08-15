using UnityEngine;

public class BushShake3 : MonoBehaviour
{
    private bool m_IsShaking = false;
    private float m_ShakeTimer = 0f;
    private int m_ShakeStep = 0;
    private Quaternion m_StartRotation;

    // Caching Quaternion values to avoid recreation
    private static readonly Quaternion SHAKE_RIGHT = Quaternion.Euler(0, 0, 8f);
    private static readonly Quaternion SHAKE_LEFT = Quaternion.Euler(0, 0, -8f);
    private static readonly Quaternion SHAKE_HALF = Quaternion.Euler(0, 0, 4f);

    private Transform m_Transform;

    private void Awake()
    {
        m_Transform = transform;
        this.enabled = false; // Tắt Update() mặc định, chỉ bật khi có Player chạm vào để tiết kiệm CPU (0 chi phí khi đứng im)
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (m_IsShaking) return;

        // Tối ưu hóa: CompareTag khá nhanh
        if (other.CompareTag("Player"))
        {
            m_IsShaking = true;
            m_StartRotation = m_Transform.localRotation;
            
            // Step 0
            m_ShakeStep = 0;
            m_ShakeTimer = 0.06f;
            
            // Rung nhẹ theo trục Z (2D tilt), không quay Y-axis (3D) làm hỏng batching của 2D Sprite
            m_Transform.localRotation = m_StartRotation * SHAKE_RIGHT;
            
            this.enabled = true; // Bật Update() để bắt đầu lắc
        }
    }

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
            this.enabled = false; // Tắt Update() khi lắc xong để giải phóng CPU hoàn toàn
        }
    }
}
