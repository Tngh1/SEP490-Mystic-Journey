using UnityEngine;
using System.Collections;
using MysticJourney.API.Core;

// Executes mono behaviour operation.
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

    // Executes consume and respawn operation.
    public void ConsumeAndRespawn()
    {
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(RespawnRoutine());
    }

    // Executes respawn routine operation.
    private IEnumerator RespawnRoutine()
    {
        if (visualRoot != null)
        {
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
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null) renderer.enabled = false;
            }
        }

        if (interactCollider == null) interactCollider = GetComponent<Collider>();
        if (interactCollider2D == null) interactCollider2D = GetComponent<Collider2D>();

        if (interactCollider != null) interactCollider.enabled = false;
        if (interactCollider2D != null) interactCollider2D.enabled = false;

        if (gameObject.scene.IsValid())
        {
            WorldSceneInteractableBootstrap.RefreshFromApi(gameObject.scene);
        }

        yield return new WaitForSeconds(respawnTime);

        if (visualRoot != null && visualRoot != this.gameObject)
        {
            visualRoot.SetActive(true);
        }
        else
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null) renderer.enabled = true;
            }
        }

        if (interactCollider != null) interactCollider.enabled = true;
        if (interactCollider2D != null) interactCollider2D.enabled = true;

        if (gameObject.scene.IsValid())
        {
            WorldSceneInteractableBootstrap.RefreshFromApi(gameObject.scene);
        }
    }
}
