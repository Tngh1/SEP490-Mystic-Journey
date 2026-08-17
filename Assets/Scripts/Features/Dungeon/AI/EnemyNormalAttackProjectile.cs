using UnityEngine;

// Executes mono behaviour operation.
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

    // Executes setup operation.
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

    // Per-frame update loop for EnemyNormalAttackProjectile.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (!_initialized) return;
        transform.position += _direction * (_speed * Time.deltaTime);
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null) return;
        HandleHit(collision.gameObject, collision.isTrigger);
    }

    // Executes on collision enter2 d operation.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;
        HandleHit(collision.gameObject, false);
    }

    // Executes handle hit operation.
    private void HandleHit(GameObject hitObj, bool isTrigger)
    {
        if (hitObj.GetComponent<EnemyEntity>() != null || hitObj.GetComponent<EnemyBehaviour>() != null || hitObj.layer == LayerMask.NameToLayer("Ignore Raycast")) return;

        if (hitObj.CompareTag("Player"))
        {
            SpawnImpactEffect();
            DealDamage(hitObj);
            Destroy(gameObject);
            return;
        }

        if (!isTrigger)
        {
            SpawnImpactEffect();
            Destroy(gameObject);
        }
    }

    // Executes spawn impact effect operation.
    private void SpawnImpactEffect()
    {
        if (impactEffectPrefab != null)
        {
            Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
        }
    }

    // Executes deal damage operation.
    private void DealDamage(GameObject playerObj)
    {
        if (EnemySkillVisualReplica.IsReplica(this)) return;

        var networkPlayer = playerObj.GetComponent<NetworkPlayer>();
        if (networkPlayer != null)
        {
            int netDamage = _isCrit ? Mathf.RoundToInt(_damage * _critMultiplier) : _damage;
            networkPlayer.RequestDamage(netDamage, _isCrit);
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
