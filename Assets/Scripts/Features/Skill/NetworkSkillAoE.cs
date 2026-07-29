using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Networked version of <see cref="SkillAoE"/>. Spawned via Runner.Spawn so every
/// client sees the AoE effect at the same world position. The effect stays put
/// (position replicated once at spawn); lifetime + hit detection run only on the
/// StateAuthority. Each enemy is damaged once; the damage number is broadcast to
/// all clients so everyone sees it.
/// </summary>
public class NetworkSkillAoE : NetworkBehaviour
{
    [SerializeField] private float duration = 3f;
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Networked] private float Damage { get; set; }
    [Networked] private TickTimer Life { get; set; }

    private readonly HashSet<Collider2D> _damaged = new HashSet<Collider2D>();

    /// <summary>Set by the caster right after Runner.Spawn (onBeforeSpawned).</summary>
    public void Configure(float damage) => Damage = damage;

    public override void Spawned()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        // The prefab also carries the legacy SkillAoE for offline play. Online,
        // THIS component owns lifetime + damage, so silence the legacy one.
        var legacy = GetComponent<SkillAoE>();
        if (legacy != null) legacy.enabled = false;

        if (HasStateAuthority)
            Life = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (Life.Expired(Runner))
            Runner.Despawn(Object);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Offline this component rides along on an Instantiate'd (never Spawned)
        // prefab where the legacy SkillAoE owns the hit — Object is null, so bail
        // before touching any networked state.
        if (Object == null || !Object.IsValid) return;
        if (!HasStateAuthority) return;
        if (!collision.CompareTag("Monster")) return;
        if (_damaged.Contains(collision)) return;

        var enemy = collision.GetComponent<EnemyEntity>();
        if (enemy == null) return;

        _damaged.Add(collision);

        bool isCrit = Random.Range(0f, 100f) <= 20f;
        int dmg = Mathf.RoundToInt(isCrit ? Damage * 1.5f : Damage);

        enemy.TakeDamage(dmg);
        RPC_ShowPopup(enemy.transform.position, dmg, isCrit);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowPopup(Vector3 worldPos, int amount, bool isCrit)
    {
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.Create(worldPos, amount, isCrit, false);
    }
}
