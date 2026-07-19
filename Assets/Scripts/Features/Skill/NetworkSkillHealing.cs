using UnityEngine;
using Fusion;

/// <summary>
/// A healing skill that finds the closest allied PlayerEntity upon spawning 
/// and heals them. If no target is found, it heals the caster.
/// </summary>
public class NetworkSkillHealing : NetworkBehaviour
{
    [Tooltip("Amount of HP to heal. Will scale with caster ATK if scaleWithAtk is true.")]
    [SerializeField] private int baseHealAmount = 50;
    
    [Tooltip("If true, actual heal = baseHealAmount + (Caster ATK * 1.5)")]
    [SerializeField] private bool scaleWithAtk = true;

    [Tooltip("VFX duration before destroying itself")]
    [SerializeField] private float duration = 1.5f;

    [Tooltip("Radius to search for an allied player around the spawn point")]
    [SerializeField] private float searchRadius = 2f;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            ApplyHeal();
        }
    }

    private void Start()
    {
        // Khi chơi offline (không qua mạng), Fusion không gọi Spawned(), 
        // Object sẽ null nên ta dùng Start() để chạy kỹ năng.
        if (Object == null) 
        {
            ApplyHeal();
        }
    }

    private Transform _targetToFollow;

    private void ApplyHeal()
    {
        // Calculate final heal amount based on the caster's ATK
        int finalHeal = baseHealAmount;
        if (scaleWithAtk && PlayerEntity.Instance != null && PlayerEntity.Instance.GetComponent<PlayerCombat>() != null)
        {
            float casterAtk = PlayerEntity.Instance.GetComponent<PlayerCombat>().TotalAttackDamage;
            finalHeal += Mathf.RoundToInt(casterAtk * 1.5f);
        }

        // Find the targets to heal.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, searchRadius);
        bool healedSomeone = false;
        
        foreach (var hit in hits)
        {
            var pEntity = hit.GetComponentInParent<PlayerEntity>();
            if (pEntity != null)
            {
                pEntity.Heal(finalHeal);
                var combat = pEntity.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    combat.AddDebuffImmunity(3f); // Buff kháng hiệu ứng 3 giây
                }
                
                // Track the healed player so the shield follows them
                _targetToFollow = pEntity.transform;
                healedSomeone = true;
                Debug.Log($"[NetworkSkillHealing] Healed {pEntity.gameObject.name} for {finalHeal} HP and applied Immunity.");
                break; // Chỉ cần bám theo 1 người
            }
        }

        // If no one is around, fallback to healing the caster
        if (!healedSomeone && PlayerEntity.Instance != null)
        {
            PlayerEntity.Instance.Heal(finalHeal);
            var combat = PlayerEntity.Instance.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                combat.AddDebuffImmunity(3f);
            }
            
            _targetToFollow = PlayerEntity.Instance.transform;
            Debug.Log($"[NetworkSkillHealing] Fallback: Healed {PlayerEntity.Instance.gameObject.name} for {finalHeal} HP and applied Immunity.");
        }

        // Despawn after duration
        Invoke(nameof(DespawnSelf), duration);
    }

    private void Update()
    {
        if (_targetToFollow != null)
        {
            // Bám sát vào người chơi được hồi máu
            transform.position = _targetToFollow.position + new Vector3(0, 0.5f, 0); // Lệch lên trên 1 chút nếu cần
        }
    }

    private void DespawnSelf()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            Runner.Despawn(Object);
        }
        else if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }
}
