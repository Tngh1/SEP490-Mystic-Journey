using UnityEngine;
using System.Collections;
using MysticJourney.API.Core;

[RequireComponent(typeof(WorldInteractable))]
public class WorldRespawnable : MonoBehaviour
{
    [Tooltip("Thời gian để quái/vật phẩm hồi sinh (tính bằng giây) sau khi bị tương tác/thu thập")]
    [SerializeField] private float respawnTime = 30f;
    
    [Tooltip("Kéo Object hiển thị (Model 3D, Sprite, Particle...) vào đây. Nếu game 2D và bạn bỏ trống ô này, nó sẽ tự động tìm và tắt ảnh (SpriteRenderer).")]
    [SerializeField] private GameObject visualRoot;
    
    [Tooltip("Kéo Collider 3D vào đây (nếu dùng 3D) để tránh người chơi tương tác lại khi chưa hồi sinh.")]
    [SerializeField] private Collider interactCollider;
    
    [Tooltip("Kéo Collider 2D vào đây (nếu dùng 2D) để tránh người chơi tương tác lại khi chưa hồi sinh.")]
    [SerializeField] private Collider2D interactCollider2D;

    public void ConsumeAndRespawn()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        // 1. Ẩn vật thể và tắt va chạm
        if (visualRoot != null) 
        {
            // Cảnh báo nếu người dùng lỡ kéo chính GameObject chứa script vào đây
            if (visualRoot == this.gameObject)
            {
                Debug.LogWarning("[WorldRespawnable] Lỗi: Đừng kéo chính nó vào Visual Root! Coroutine sẽ bị dừng và không hồi sinh được. Hãy để trống ô Visual Root.");
            }
            else
            {
                visualRoot.SetActive(false);
            }
        }
        else
        {
            // Tự động tắt ảnh (SpriteRenderer) nếu để trống
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.enabled = false;
        }
        
        // Tự động tìm Collider nếu chưa gán
        if (interactCollider == null) interactCollider = GetComponent<Collider>();
        if (interactCollider2D == null) interactCollider2D = GetComponent<Collider2D>();

        if (interactCollider != null) interactCollider.enabled = false;
        if (interactCollider2D != null) interactCollider2D.enabled = false;

        // Báo cho hệ thống quét của Player biết để gỡ chữ "Press E" xuống ngay lập tức
        if (gameObject.scene.IsValid())
        {
            WorldSceneInteractableBootstrap.RefreshFromApi(gameObject.scene);
        }

        // 2. Chờ thời gian hồi sinh
        yield return new WaitForSeconds(respawnTime);

        // 3. Hiện lại vật thể và bật va chạm
        if (visualRoot != null && visualRoot != this.gameObject) 
        {
            visualRoot.SetActive(true);
        }
        else
        {
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.enabled = true;
        }

        if (interactCollider != null) interactCollider.enabled = true;
        if (interactCollider2D != null) interactCollider2D.enabled = true;
        
        if (gameObject.scene.IsValid())
        {
            WorldSceneInteractableBootstrap.RefreshFromApi(gameObject.scene);
        }
    }
}
