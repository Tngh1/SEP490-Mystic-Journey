using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Fusion authority wrapper for a dungeon enemy.
///
/// The existing <see cref="EnemyEntity"/> / <see cref="EnemyBehaviour"/> /
/// <see cref="EnemyAnimations"/> stay plain MonoBehaviours that own the *local*
/// gameplay logic (HP maths, NavMesh AI, animation). This component makes that
/// logic authoritative + replicated when a Photon session is running:
///
///   • The client that spawned the enemy (the Shared-Mode master client) holds
///     StateAuthority. It ALONE runs the AI (NavMeshAgent) and the real HP maths
///     in EnemyEntity, then copies the result into [Networked] fields each tick.
///   • Every other client is a proxy: its EnemyBehaviour + NavMeshAgent are
///     disabled (position arrives via NetworkTransform) and it mirrors the
///     replicated HP / alive state into EnemyEntity so the health bar, hit
///     flashes and death animation match the authority.
///   • Damage requests from ANY client (its projectile / AoE / melee hit the
///     enemy) are routed to the authority via RPC, so damage is applied exactly
///     once and everyone sees the same HP and death.
///
/// Offline (no Runner / Photon not running) this component stays inert —
/// Spawned() never fires, so EnemyEntity behaves exactly as before.
/// </summary>
[RequireComponent(typeof(EnemyEntity))]
public class NetworkEnemy : NetworkBehaviour, IStateAuthorityChanged
{
    // Authoritative, replicated state. Written only by the StateAuthority.
    [Networked] public int CurrentHp { get; set; }
    [Networked] public int MaxHp { get; set; }
    [Networked] public NetworkBool IsAlive { get; set; }

    private EnemyEntity _entity;
    private EnemyBehaviour _behaviour;
    private NavMeshAgent _agent;
    private readonly Dictionary<string, GameObject> _skillPrefabs = new(System.StringComparer.Ordinal);

    // Proxy-side mirror bookkeeping so we only push changes into EnemyEntity.
    private int _lastMirroredHp = int.MinValue;
    private int _lastMirroredMaxHp = int.MinValue;
    private bool _lastMirroredAlive = true;

    /// <summary>
    /// True when this enemy is a live networked object inside a running session.
    /// EnemyEntity uses this to decide between the networked damage route and the
    /// offline local-damage path.
    /// </summary>
    public bool IsNetworkActive =>
        Object != null && Object.IsValid && Runner != null && Runner.IsRunning;

    private void Awake()
    {
        _entity = GetComponent<EnemyEntity>();
        _behaviour = GetComponent<EnemyBehaviour>();
        _agent = GetComponent<NavMeshAgent>();
    }

    public override void Spawned()
    {
        // Let EnemyEntity know it is now networked so TakeDamage routes to us.
        _entity.BindNetwork(this);

        if (HasStateAuthority)
        {
            // The authority runs the real EnemyEntity HP logic. Seed the mirror
            // from its starting values (EnemyEntity.Start may set these from API).
            MaxHp = Mathf.Max(1, _entity.MaxHealth);
            CurrentHp = _entity.CurrentHealth > 0 ? _entity.CurrentHealth : MaxHp;
            IsAlive = true;
        }

        ApplyAuthorityRole();

        Debug.Log($"[NetworkEnemy] Spawned '{name}' authority={HasStateAuthority} pos={transform.position} " +
                  $"scene='{gameObject.scene.name}'");

        // Notify DungeonManager so UI tracks progress even on proxy clients
        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.RegisterNetworkedEnemy(_entity);
        }
    }

    /// <summary>
    /// Fusion hands StateAuthority over this enemy to another client when the
    /// previous authority leaves the session. Without re-running the AI enable/disable
    /// here, the enemy kept its proxy setup on the new authority — EnemyBehaviour and
    /// the NavMeshAgent stayed disabled and every enemy froze in place for the rest of
    /// the run.
    /// </summary>
    public void StateAuthorityChanged()
    {
        if (HasStateAuthority)
        {
            // Adopt whatever the replicated state says so the fresh authority does not
            // publish a stale/default HP on its first tick.
            _entity.SyncNetworkedHealth(CurrentHp, MaxHp);
        }

        ApplyAuthorityRole();
    }

    /// <summary>
    /// AI + navmesh run on the StateAuthority only; proxies take position from
    /// NetworkTransform instead, so local AI there would fight the replication.
    /// </summary>
    private void ApplyAuthorityRole()
    {
        bool authority = HasStateAuthority;
        if (_behaviour != null) _behaviour.enabled = authority;
        if (_agent != null) _agent.enabled = authority;
        SetAuthorityOnlyComponent<ExtraEnemySkillSpawner>(authority);
        SetAuthorityOnlyComponent<DragonAttackShooter>(authority);
        SetAuthorityOnlyComponent<IceFairySupportAI>(authority);
        SetAuthorityOnlyComponent<SwampDemonSlimeSpawner>(authority);
    }

    private void SetAuthorityOnlyComponent<T>(bool enabled) where T : UnityEngine.Behaviour
    {
        var component = GetComponent<T>();
        if (component != null) component.enabled = enabled;
    }

    public void RegisterSkillPrefab(GameObject prefab)
    {
        if (prefab != null) _skillPrefabs[prefab.name] = prefab;
    }

    public GameObject SpawnEnemySkill(GameObject prefab, Vector3 position, bool parentToEnemy = false)
    {
        if (prefab == null) return null;
        RegisterSkillPrefab(prefab);

        var instance = Instantiate(prefab, position, Quaternion.identity);
        if (parentToEnemy) instance.transform.SetParent(transform, true);

        if (IsNetworkActive && HasStateAuthority)
            RPC_SpawnEnemySkill(prefab.name, position, parentToEnemy);

        return instance;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnEnemySkill(string prefabName, Vector3 position, NetworkBool parentToEnemy)
    {
        if (HasStateAuthority) return;
        if (!_skillPrefabs.TryGetValue(prefabName, out var prefab) || prefab == null)
        {
            Debug.LogWarning($"[NetworkEnemy] Replica prefab '{prefabName}' is not registered on {name}.");
            return;
        }

        var instance = Instantiate(prefab, position, Quaternion.identity);
        instance.AddComponent<EnemySkillVisualReplica>();
        if (parentToEnemy) instance.transform.SetParent(transform, true);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Copy the authority's local EnemyEntity HP into the networked mirror so
        // it replicates to every proxy. EnemyEntity remains the source of truth on
        // the authority; we just publish it.
        //
        // Only write on change: assigning a [Networked] property marks it dirty even
        // when the value is identical, so the old unconditional writes re-sent HP for
        // every enemy every tick (idle enemies included).
        int maxHp = Mathf.Max(1, _entity.MaxHealth);
        int currentHp = Mathf.Max(0, _entity.CurrentHealth);
        bool alive = !_entity.IsDead;

        if (MaxHp != maxHp) MaxHp = maxHp;
        if (CurrentHp != currentHp) CurrentHp = currentHp;
        if (IsAlive != alive) IsAlive = alive;
    }

    public override void Render()
    {
        // The authority already shows its own local EnemyEntity state directly.
        if (HasStateAuthority) return;

        // Proxy mirror: push replicated HP into EnemyEntity so the health bar and
        // hit flash match the authority.
        if (CurrentHp != _lastMirroredHp || MaxHp != _lastMirroredMaxHp)
        {
            _entity.SyncNetworkedHealth(CurrentHp, MaxHp);
            _lastMirroredHp = CurrentHp;
            _lastMirroredMaxHp = MaxHp;
        }

        // Replicated death → play death visuals on this proxy exactly once.
        if (!IsAlive && _lastMirroredAlive)
        {
            _lastMirroredAlive = false;
            _entity.SyncNetworkedDeath();
        }
    }

    /// <summary>
    /// Request that this enemy takes damage. Safe to call on any client — it is
    /// routed to the StateAuthority, which applies it once. Callers that are
    /// certain they are offline should use <see cref="EnemyEntity.TakeDamage"/>.
    /// </summary>
    public void RequestDamage(int amount)
    {
        if (amount <= 0) return;

        if (HasStateAuthority)
            _entity.ApplyDamageAuthoritative(amount);
        else
            RPC_RequestDamage(amount);
    }

    // Any client (a player whose projectile/AoE hit this enemy) → the enemy's
    // StateAuthority. Shared Mode allows RpcSources.All to target StateAuthority.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDamage(int amount)
    {
        _entity.ApplyDamageAuthoritative(amount);
    }

    /// <summary>
    /// Broadcast a floating damage number to EVERY client. Any client may invoke it
    /// (Shared Mode RpcSources.All → RpcTargets.All), and all clients — including the
    /// caller — spawn the popup once. Used by melee, which (unlike the networked skill
    /// projectiles) has no networked object of its own to broadcast from, so the damage
    /// number would otherwise only appear on the attacker's screen.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ShowDamagePopup(Vector3 worldPos, int amount, NetworkBool isCrit)
    {
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.Create(worldPos, amount, isCrit, false);
    }
}

public sealed class EnemySkillVisualReplica : MonoBehaviour
{
    public static bool IsReplica(Component component) =>
        component != null && component.GetComponentInParent<EnemySkillVisualReplica>() != null;
}
