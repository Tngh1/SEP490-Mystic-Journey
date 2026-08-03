using UnityEngine;

/// <summary>
/// Component điều khiển chiêu/đạn đánh thường bay ra từ quái đánh xa.
/// Gây sát thương khi trúng Player và tự nổ/huỷ khi đâm vào tường hoặc vật cản môi trường.
/// </summary>
public class EnemyNormalAttackProjectile : MonoBehaviour
{
    [Tooltip("Hiệu ứng nổ/va chạm khi đạn chạm người chơi hoặc tường vật cản (nếu có)")]
    [SerializeField] private GameObject impactEffectPrefab;

    private Vector3 _direction;
    private float _speed = 8f;
    private int _damage = 10;
    private bool _isCrit = false;
    private float _critMultiplier = 1.5f;
    private float _lifeTime = 3.5f;
    private bool _initialized = false;

    public void Setup(Vector3 direction, float speed, int damage, bool isCrit, float critMultiplier)
    {
        _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
        _speed = speed > 0 ? speed : 8f;
        _damage = damage;
        _isCrit = isCrit;
        _critMultiplier = critMultiplier;
        _initialized = true;

        Destroy(gameObject, _lifeTime);

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private void Update()
    {
        if (!_initialized) return;
        transform.position += _direction * (_speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null) return;
        HandleHit(collision.gameObject, collision.isTrigger);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;
        HandleHit(collision.gameObject, false);
    }

    private void HandleHit(GameObject hitObj, bool isTrigger)
    {
        // 1. Bỏ qua va chạm với quái/kẻ địch/spawner
        if (hitObj.GetComponent<EnemyEntity>() != null || hitObj.GetComponent<EnemyBehaviour>() != null || hitObj.layer == LayerMask.NameToLayer("Ignore Raycast")) return;

        // 2. Nếu là Player -> Gây sát thương, tạo hiệu ứng va chạm và huỷ đạn
        if (hitObj.CompareTag("Player"))
        {
            SpawnImpactEffect();
            DealDamage(hitObj);
            Destroy(gameObject);
            return;
        }

        // 3. Nếu chạm phải vật thể cản cứng (Tường, Đá, Cây, Tilemap, Object môi trường...) không phải Trigger
        if (!isTrigger)
        {
            SpawnImpactEffect();
            Destroy(gameObject);
        }
    }

    private void SpawnImpactEffect()
    {
        if (impactEffectPrefab != null)
        {
            Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
        }
    }

    private void DealDamage(GameObject playerObj)
    {
        var networkPlayer = playerObj.GetComponent<NetworkPlayer>();
        if (networkPlayer != null)
        {
            int netDamage = _isCrit ? Mathf.RoundToInt(_damage * _critMultiplier) : _damage;
            networkPlayer.RequestDamage(netDamage);
        }
        else
        {
            var playerEntity = playerObj.GetComponent<PlayerEntity>();
            if (playerEntity != null)
            {
                playerEntity.TakeDamage(_damage, _isCrit, _critMultiplier);
            }
            else if (PlayerEntity.Instance != null)
            {
                PlayerEntity.Instance.TakeDamage(_damage, _isCrit, _critMultiplier);
            }
        }
    }
}
