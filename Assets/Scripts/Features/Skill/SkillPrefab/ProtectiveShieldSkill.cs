using UnityEngine;

public class ProtectiveShieldSkill : MonoBehaviour
{
    [SerializeField] private float radius = 5f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float defenseShareRatio = 0.5f;

    private void Start()
    {
        PlayerCombat casterCombat = null;
        
        // Find the caster (the local player where this was spawned)
        Collider2D[] casterHits = Physics2D.OverlapCircleAll(transform.position, 0.5f);
        foreach (var hit in casterHits)
        {
            var combat = hit.GetComponent<PlayerCombat>();
            if (combat != null && combat.gameObject.CompareTag("Player"))
            {
                casterCombat = combat;
                break;
            }
        }

        float casterDef = casterCombat != null ? casterCombat.TotalDef : 0f;
        float buffAmount = casterDef * defenseShareRatio;

        // Apply to all players in radius
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                var player = hit.GetComponent<PlayerCombat>();
                if (player != null)
                {
                    player.AddDefBuff(buffAmount, duration);
                    player.AddDebuffImmunity(duration);
                    
                    // Show a text popup for buff
                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.Create(player.transform.position + Vector3.up * 1f, (int)buffAmount, false, false);
                    }
                }
            }
        }

        Destroy(gameObject, 2f);
    }
}
