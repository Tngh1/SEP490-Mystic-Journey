using Fusion;
using UnityEngine;

// Executes network behaviour operation.
public class NetworkSkillProjectile : NetworkBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeSeconds = 2f;

    [Networked] private float Damage { get; set; }
    [Networked] private TickTimer Life { get; set; }

    // Executes configure operation.
    public void Configure(float damage, float speedOverride)
    {
        Damage = damage;
        if (speedOverride > 0f) speed = speedOverride;
    }

    // Fusion lifecycle callback invoked when this NetworkSkillProjectile NetworkObject is spawned into the network session.
    // Configures input/state authority handlers, sets singleton references if local player, and applies initial visuals.
    public override void Spawned()
    {
        var legacy = GetComponent<SkillProjectile>();
        if (legacy != null) legacy.enabled = false;

        if (HasStateAuthority)
            Life = TickTimer.CreateFromSeconds(Runner, lifeSeconds);
    }

    // Networked fixed-step simulation tick callback executed by Photon Fusion.
    // Processes synchronized player input, applies physics velocities, and updates authoritative gameplay mechanics.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        transform.position += transform.right * speed * Runner.DeltaTime;

        if (Life.Expired(Runner))
            Runner.Despawn(Object);
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (Object == null || !Object.IsValid) return;
        if (!HasStateAuthority) return;

        if (collision.CompareTag("Player")) return;

        var enemy = collision.GetComponentInParent<EnemyEntity>();

        if (collision.isTrigger && enemy == null && !collision.CompareTag("Monster")) return;

        if (enemy != null || collision.CompareTag("Monster"))
        {
            if (enemy != null)
            {
                // Randomize the eligible candidates before selecting this gameplay result.
                bool isCrit = Random.Range(0f, 100f) <= 20f;
                int dmg = Mathf.RoundToInt(isCrit ? Damage * 1.5f : Damage);

                enemy.TakeDamage(dmg);
                RPC_ShowPopup(enemy.transform.position, dmg, isCrit);
            }
            Runner.Despawn(Object);
            return;
        }

        Runner.Despawn(Object);
    }

    // Executes on collision enter2 d operation.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        OnTriggerEnter2D(collision.collider);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_show popup operation.
    private void RPC_ShowPopup(Vector3 worldPos, int amount, bool isCrit)
    {
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.Create(worldPos, amount, isCrit, false);
    }
}
