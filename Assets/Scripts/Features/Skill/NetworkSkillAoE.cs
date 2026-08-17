using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Executes network behaviour operation.
public class NetworkSkillAoE : NetworkBehaviour
{
    [SerializeField] private float duration = 3f;
    [SerializeField] private AudioClip castSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Networked] private float Damage { get; set; }
    [Networked] private TickTimer Life { get; set; }

    private readonly HashSet<Collider2D> _damaged = new HashSet<Collider2D>();

    // Executes configure operation.
    public void Configure(float damage) => Damage = damage;

    // Fusion lifecycle callback invoked when this NetworkSkillAoE NetworkObject is spawned into the network session.
    // Configures input/state authority handlers, sets singleton references if local player, and applies initial visuals.
    public override void Spawned()
    {
        if (castSound != null && MysticJourney.Core.Services.AudioManager.Instance != null)
        {
            MysticJourney.Core.Services.AudioManager.Instance.PlaySfx(castSound, soundVolume);
        }

        var legacy = GetComponent<SkillAoE>();
        if (legacy != null) legacy.enabled = false;

        if (HasStateAuthority)
            Life = TickTimer.CreateFromSeconds(Runner, duration);
    }

    // Networked fixed-step simulation tick callback executed by Photon Fusion.
    // Processes synchronized player input, applies physics velocities, and updates authoritative gameplay mechanics.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (Life.Expired(Runner))
            Runner.Despawn(Object);
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (Object == null || !Object.IsValid) return;
        if (!HasStateAuthority) return;
        if (_damaged.Contains(collision)) return;

        var enemy = collision.GetComponentInParent<EnemyEntity>();
        if (enemy == null && !collision.CompareTag("Monster")) return;

        if (enemy == null) return;

        _damaged.Add(collision);

        // Randomize the eligible candidates before selecting this gameplay result.
        bool isCrit = Random.Range(0f, 100f) <= 20f;
        int dmg = Mathf.RoundToInt(isCrit ? Damage * 1.5f : Damage);

        enemy.TakeDamage(dmg);
        RPC_ShowPopup(enemy.transform.position, dmg, isCrit);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_show popup operation.
    private void RPC_ShowPopup(Vector3 worldPos, int amount, bool isCrit)
    {
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.Create(worldPos, amount, isCrit, false);
    }
}
