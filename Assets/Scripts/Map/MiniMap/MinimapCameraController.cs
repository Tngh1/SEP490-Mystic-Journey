using UnityEngine;

public class MinimapCameraController : MonoBehaviour
{
    private Transform playerTarget;
    private float cameraZOffset = -10f;
    private bool isReady = false; // C? ?ánh d?u khi nào có data m?i ch?y

    // Hàm này sau này s? nh?n data t? file Test ho?c JSON API
    public void InitializeMinimap(Transform targetTransform)
    {
        playerTarget = targetTransform;

        // Set v? trí camera ngay l?p t?c theo v? trí data truy?n vào
        if (playerTarget != null)
        {
            Vector3 startPos = playerTarget.position;
            startPos.z = cameraZOffset;
            transform.position = startPos;

            isReady = true; // B?t c? cho phép camera b?t ??u ?i theo
            Debug.Log("? [Minimap] ?ã nh?n d? li?u kh?i t?o thành công!");
        }
    }

    private void LateUpdate()
    {
        // N?u ch?a có data t? API/Test b?m vào, ho?c m?t target -> ??ng im
        if (!isReady || playerTarget == null) return;

        // C?p nh?t v? trí m??t mà (có th? dùng Lerp sau này n?u thích)
        Vector3 newPosition = playerTarget.position;
        newPosition.z = cameraZOffset;
        transform.position = newPosition;
    }
}