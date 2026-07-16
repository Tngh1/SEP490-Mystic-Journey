using UnityEngine;

public class EnemySkillDamage : MonoBehaviour
{
    [Tooltip("Sát thương của kỹ năng này")]
    [SerializeField] private int damage = 10;
    
    [Tooltip("Thời gian tồn tại của kỹ năng (tính bằng giây) trước khi tự động biến mất")]
    [SerializeField] private float lifeTime = 3f;
    
    [Tooltip("Có huỷ kỹ năng ngay sau khi chạm vào người chơi không?")]
    [SerializeField] private bool destroyOnHit = true;

    private void Start()
    {
        // Tự động huỷ sau một khoảng thời gian (lifeTime) để dọn dẹp các khối băng cũ
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Kiểm tra xem đối tượng va chạm có phải là Player không
        if (collision.CompareTag("Player"))
        {
            DealDamage(collision.gameObject);
            
            // Huỷ cục băng nếu được thiết lập
            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Xử lý tương tự nếu dùng va chạm vật lý (không tick Is Trigger)
        if (collision.gameObject.CompareTag("Player"))
        {
            DealDamage(collision.gameObject);

            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
    }

    private void DealDamage(GameObject target)
    {
        // Xử lý sát thương tương tự như cách Boss đánh thường
        var networkPlayer = target.GetComponent<NetworkPlayer>();
        if (networkPlayer != null)
        {
            networkPlayer.RequestDamage(damage);
        }
        else
        {
            var playerEntity = target.GetComponent<PlayerEntity>();
            if (playerEntity != null)
            {
                playerEntity.TakeDamage(damage);
            }
            else if (PlayerEntity.Instance != null)
            {
                PlayerEntity.Instance.TakeDamage(damage);
            }
        }
    }
}
