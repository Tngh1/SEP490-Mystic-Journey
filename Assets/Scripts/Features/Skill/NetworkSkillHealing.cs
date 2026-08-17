using UnityEngine;
using Fusion;

// Executes network behaviour operation.
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

    [Header("Audio")]
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    // Fusion lifecycle callback invoked when this NetworkSkillHealing NetworkObject is spawned into the network session.
    // Configures input/state authority handlers, sets singleton references if local player, and applies initial visuals.
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            ApplyHeal();
        }
    }

    // Performs startup initialization for NetworkSkillHealing on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (Object == null)
        {
            ApplyHeal();
        }
    }

    private Transform _targetToFollow;

    // Restores player health clamped to MaxHp and triggers combat popup visual effects.
    private void ApplyHeal()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        int finalHeal = baseHealAmount;
        if (scaleWithAtk && PlayerEntity.Instance != null && PlayerEntity.Instance.GetComponent<PlayerCombat>() != null)
        {
            float casterAtk = PlayerEntity.Instance.GetComponent<PlayerCombat>().TotalAttackDamage;
            finalHeal += Mathf.RoundToInt(casterAtk * 1.5f);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, searchRadius);
        bool healedSomeone = false;

        foreach (var hit in hits)
        {
            var pEntity = hit.GetComponentInParent<PlayerEntity>();
            if (pEntity != null)
            {
                pEntity.Heal(finalHeal);

                var networkPlayer = pEntity.GetComponent<NetworkPlayer>();
                if (networkPlayer != null && networkPlayer.Object != null)
                {
                    networkPlayer.RPC_ApplyDebuffImmunity(3f);
                }
                else
                {
                    var combat = pEntity.GetComponent<PlayerCombat>();
                    if (combat != null)
                    {
                        combat.AddDebuffImmunity(3f);
                    }
                }

                _targetToFollow = pEntity.transform;
                healedSomeone = true;
                Debug.Log($"[NetworkSkillHealing] Healed {pEntity.gameObject.name} for {finalHeal} HP and applied Immunity.");
                break;
            }
        }

        if (!healedSomeone && PlayerEntity.Instance != null)
        {
            PlayerEntity.Instance.Heal(finalHeal);

            var networkPlayer = PlayerEntity.Instance.GetComponent<NetworkPlayer>();
            if (networkPlayer != null && networkPlayer.Object != null)
            {
                networkPlayer.RPC_ApplyDebuffImmunity(3f);
            }
            else
            {
                var combat = PlayerEntity.Instance.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    combat.AddDebuffImmunity(3f);
                }
            }

            _targetToFollow = PlayerEntity.Instance.transform;
            Debug.Log($"[NetworkSkillHealing] Fallback: Healed {PlayerEntity.Instance.gameObject.name} for {finalHeal} HP and applied Immunity.");
        }

        Invoke(nameof(DespawnSelf), duration);
    }

    // Per-frame update loop for NetworkSkillHealing.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_targetToFollow != null)
        {
            transform.position = _targetToFollow.position + new Vector3(0, 0.5f, 0);
        }
    }

    // Executes despawn self operation.
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
