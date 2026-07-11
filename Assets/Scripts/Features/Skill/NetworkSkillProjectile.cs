using Fusion;
using UnityEngine;

/// <summary>
/// Networked version of <see cref="SkillProjectile"/>. Spawned via Runner.Spawn
/// so every client sees the projectile fly. Movement + lifetime run in
/// FixedUpdateNetwork on the StateAuthority (the caster in Shared Mode);
/// NetworkTransform replicates the position to all proxies. Hit detection and
/// damage are applied only on the StateAuthority and routed to the enemy's own
/// authority via <see cref="EnemyEntity.TakeDamage"/> (which forwards to
/// NetworkEnemy). Every client shows its own damage popup via a broadcast RPC.
///
/// The prefab keeps its 2D trigger collider; only the authority acts on the
/// trigger so damage is applied exactly once.
/// </summary>
public class NetworkSkillProjectile : NetworkBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeSeconds = 2f;

    [Networked] private float Damage { get; set; }
    [Networked] private TickTimer Life { get; set; }

    /// <summary>Set by the caster right after Runner.Spawn (onBeforeSpawned).</summary>
    public void Configure(float damage, float speedOverride)
    {
        Damage = damage;
        if (speedOverride > 0f) speed = speedOverride;
    }

    public override void Spawned()
    {
        // The prefab also carries the legacy SkillProjectile for offline play.
        // Online, THIS component owns movement + damage, so silence the legacy
        // one to avoid double movement / double damage.
        var legacy = GetComponent<SkillProjectile>();
        if (legacy != null) legacy.enabled = false;

        if (HasStateAuthority)
            Life = TickTimer.CreateFromSeconds(Runner, lifeSeconds);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Move along local +X (prefab is rotated toward the aim on spawn).
        transform.position += transform.right * speed * Runner.DeltaTime;

        if (Life.Expired(Runner))
            Runner.Despawn(Object);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Offline this component rides along on an Instantiate'd (never Spawned)
        // prefab where the legacy SkillProjectile owns the hit — Object is null,
        // so bail before touching any networked state.
        if (Object == null || !Object.IsValid) return;
        // Only the authority resolves hits so damage is applied exactly once.
        if (!HasStateAuthority) return;
        if (!collision.CompareTag("Monster")) return;

        var enemy = collision.GetComponent<EnemyEntity>();
        if (enemy == null) return;

        bool isCrit = Random.Range(0f, 100f) <= 20f;
        int dmg = Mathf.RoundToInt(isCrit ? Damage * 1.5f : Damage);

        enemy.TakeDamage(dmg);
        RPC_ShowPopup(enemy.transform.position, dmg, isCrit);

        Runner.Despawn(Object);
    }

    // Broadcast so the floating damage number appears on every client, not just
    // the one that owns the enemy authority.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowPopup(Vector3 worldPos, int amount, bool isCrit)
    {
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.Create(worldPos, amount, isCrit, false);
    }
}
