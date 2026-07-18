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
        else if (Runner.IsSinglePlayer) // Fallback for offline mode if needed
        {
            ApplyHeal();
        }
    }

    private void ApplyHeal()
    {
        // Calculate final heal amount based on the caster's ATK
        int finalHeal = baseHealAmount;
        if (scaleWithAtk && PlayerEntity.Instance != null && PlayerEntity.Instance.GetComponent<PlayerCombat>() != null)
        {
            float casterAtk = PlayerEntity.Instance.GetComponent<PlayerCombat>().TotalAttackDamage;
            finalHeal += Mathf.RoundToInt(casterAtk * 1.5f);
        }

        // Find the target to heal.
        // It should spawn directly AT the chosen target's position, so we check a small radius.
        PlayerEntity targetToHeal = null;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, searchRadius);
        
        float minDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            var pEntity = hit.GetComponentInParent<PlayerEntity>();
            if (pEntity != null)
            {
                float dist = Vector3.Distance(transform.position, pEntity.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    targetToHeal = pEntity;
                }
            }
        }

        // If no one is around, fallback to healing the caster
        if (targetToHeal == null)
        {
            targetToHeal = PlayerEntity.Instance;
        }

        if (targetToHeal != null)
        {
            targetToHeal.Heal(finalHeal);
            Debug.Log($"[NetworkSkillHealing] Healed {targetToHeal.gameObject.name} for {finalHeal} HP.");
        }

        // Despawn after duration
        Invoke(nameof(DespawnSelf), duration);
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
