using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Executes i state authority changed operation.
[RequireComponent(typeof(EnemyEntity))]
public class NetworkEnemy : NetworkBehaviour, IStateAuthorityChanged
{
    [Networked] public int CurrentHp { get; set; }
    [Networked] public int MaxHp { get; set; }
    [Networked] public NetworkBool IsAlive { get; set; }

    private EnemyEntity _entity;
    private EnemyBehaviour _behaviour;
    private EnemyAnimations _animations;
    private NavMeshAgent _agent;
    private readonly Dictionary<string, GameObject> _skillPrefabs = new(System.StringComparer.Ordinal);

    private int _lastMirroredHp = int.MinValue;
    private int _lastMirroredMaxHp = int.MinValue;
    private bool _lastMirroredAlive = true;

    // Executes is network active operation.
    public bool IsNetworkActive =>
        Object != null && Object.IsValid && Runner != null && Runner.IsRunning;

    // Initializes internal component caches and dependencies for NetworkEnemy upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _entity = GetComponent<EnemyEntity>();
        _behaviour = GetComponent<EnemyBehaviour>();
        _animations = GetComponent<EnemyAnimations>() ?? GetComponentInChildren<EnemyAnimations>();
        _agent = GetComponent<NavMeshAgent>();
    }

    // Fusion lifecycle callback invoked when this NetworkEnemy NetworkObject is spawned into the network session.
    // Configures input/state authority handlers, sets singleton references if local player, and applies initial visuals.
    public override void Spawned()
    {
        _entity.BindNetwork(this);

        if (HasStateAuthority)
        {
            MaxHp = Mathf.Max(1, _entity.MaxHealth);
            CurrentHp = _entity.CurrentHealth > 0 ? _entity.CurrentHealth : MaxHp;
            IsAlive = true;
        }

        ApplyAuthorityRole();

        Debug.Log($"[NetworkEnemy] Spawned '{name}' authority={HasStateAuthority} pos={transform.position} " +
                  $"scene='{gameObject.scene.name}'");

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.RegisterNetworkedEnemy(_entity);
        }
    }

    // Executes state authority changed operation.
    public void StateAuthorityChanged()
    {
        if (HasStateAuthority)
        {
            _entity.SyncNetworkedHealth(CurrentHp, MaxHp);
        }

        ApplyAuthorityRole();
    }

    // Executes apply authority role operation.
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

    // Executes behaviour operation.
    private void SetAuthorityOnlyComponent<T>(bool enabled) where T : UnityEngine.Behaviour
    {
        var component = GetComponent<T>();
        if (component != null) component.enabled = enabled;
    }

    // Executes register skill prefab operation.
    public void RegisterSkillPrefab(GameObject prefab)
    {
        if (prefab != null) _skillPrefabs[prefab.name] = prefab;
    }

    // Executes spawn enemy skill operation.
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

    // Executes resolve skill prefab operation.
    // Validates input parameters against null or empty values.
    private GameObject ResolveSkillPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;
        if (_skillPrefabs.TryGetValue(prefabName, out var registered) && registered != null)
            return registered;

        foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate != null && candidate.name == prefabName && !candidate.scene.IsValid())
            {
                _skillPrefabs[prefabName] = candidate;
                return candidate;
            }
        }

        return null;
    }

    // Create enemy projectile using prefab, position, direction, and speed; it creates enemy projectile and guards invalid or unavailable states.
    public GameObject SpawnEnemyProjectile(GameObject prefab, Vector3 position, Vector3 direction,
        float speed, int damage, bool isCrit, float critMultiplier)
    {
        if (prefab != null) RegisterSkillPrefab(prefab);

        GameObject instance = CreateEnemyProjectile(prefab, position, direction, speed, damage,
            isCrit, critMultiplier, false);

        if (IsNetworkActive && HasStateAuthority)
        {
            RPC_SpawnEnemyProjectile(
                prefab != null ? prefab.name : string.Empty,
                position,
                direction,
                speed,
                isCrit,
                critMultiplier);
        }

        return instance;
    }

    // Create enemy projectile using prefab, position, direction, and speed; it instantiates the required Unity object, creates component, updates active, loads component, and updates up and guards invalid or unavailable states.
    private GameObject CreateEnemyProjectile(GameObject prefab, Vector3 position, Vector3 direction,
        float speed, int damage, bool isCrit, float critMultiplier, bool visualOnly)
    {
        GameObject instance;
        if (prefab != null)
        {
            instance = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            instance = new GameObject($"{gameObject.name}_Projectile");
            instance.transform.position = position;
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 0.6f, 0.1f);
            var collider = instance.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.25f;
        }

        if (visualOnly)
        {
            instance.SetActive(false);
            instance.AddComponent<EnemySkillVisualReplica>();
        }

        var projectile = instance.GetComponent<EnemyNormalAttackProjectile>();
        if (projectile == null) projectile = instance.AddComponent<EnemyNormalAttackProjectile>();
        projectile.Setup(direction, speed, visualOnly ? 0 : damage, isCrit, critMultiplier);
        if (visualOnly) instance.SetActive(true);
        return instance;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Process rpc spawn enemy projectile using prefab name, position, direction, and speed; it builds skill prefab and creates enemy projectile and guards invalid or unavailable states.
    private void RPC_SpawnEnemyProjectile(string prefabName, Vector3 position, Vector3 direction,
        float speed, NetworkBool isCrit, float critMultiplier)
    {
        if (HasStateAuthority) return;

        var prefab = ResolveSkillPrefab(prefabName);
        CreateEnemyProjectile(prefab, position, direction, speed, 0, isCrit, critMultiplier, true);
    }

    // Executes notify attack animation operation.
    public void NotifyAttackAnimation()
    {
        if (IsNetworkActive && HasStateAuthority) RPC_PlayAttackAnimation();
    }

    // Executes notify skill animation operation.
    public void NotifySkillAnimation()
    {
        if (IsNetworkActive && HasStateAuthority) RPC_PlaySkillAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_play attack animation operation.
    private void RPC_PlayAttackAnimation()
    {
        if (!HasStateAuthority) _animations?.PlayAttackAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_play skill animation operation.
    private void RPC_PlaySkillAnimation()
    {
        if (!HasStateAuthority) _animations?.PlaySkillAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_spawn enemy skill operation.
    private void RPC_SpawnEnemySkill(string prefabName, Vector3 position, NetworkBool parentToEnemy)
    {
        if (HasStateAuthority) return;
        var prefab = ResolveSkillPrefab(prefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"[NetworkEnemy] Replica prefab '{prefabName}' is not registered on {name}.");
            return;
        }

        var instance = Instantiate(prefab, position, Quaternion.identity);
        instance.SetActive(false);
        instance.AddComponent<EnemySkillVisualReplica>();
        if (parentToEnemy) instance.transform.SetParent(transform, true);
        instance.SetActive(true);
    }

    // Networked fixed-step simulation tick callback executed by Photon Fusion.
    // Processes synchronized player input, applies physics velocities, and updates authoritative gameplay mechanics.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        int maxHp = Mathf.Max(1, _entity.MaxHealth);
        int currentHp = Mathf.Max(0, _entity.CurrentHealth);
        bool alive = !_entity.IsDead;

        if (MaxHp != maxHp) MaxHp = maxHp;
        if (CurrentHp != currentHp) CurrentHp = currentHp;
        if (IsAlive != alive) IsAlive = alive;
    }

    // Executes render operation.
    public override void Render()
    {
        if (HasStateAuthority) return;

        if (CurrentHp != _lastMirroredHp || MaxHp != _lastMirroredMaxHp ||
            _entity.CurrentHealth != CurrentHp || _entity.MaxHealth != MaxHp)
        {
            _entity.SyncNetworkedHealth(CurrentHp, MaxHp);
            _lastMirroredHp = CurrentHp;
            _lastMirroredMaxHp = MaxHp;
        }

        if (!IsAlive && _lastMirroredAlive)
        {
            _lastMirroredAlive = false;
            _entity.SyncNetworkedDeath();
        }
    }

    // Requests damage to be applied to this player, routing via RPC to the StateAuthority instance.
    public void RequestDamage(int amount)
    {
        if (amount <= 0) return;

        if (HasStateAuthority)
            _entity.ApplyDamageAuthoritative(amount);
        else
            RPC_RequestDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Photon Fusion RPC receiving damage request on the StateAuthority and executing ApplyDamage().
    private void RPC_RequestDamage(int amount)
    {
        _entity.ApplyDamageAuthoritative(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    // Executes rpc_show damage popup operation.
    public void RPC_ShowDamagePopup(Vector3 worldPos, int amount, NetworkBool isCrit)
    {
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.Create(worldPos, amount, isCrit, false);
    }
}

// Executes mono behaviour operation.
public sealed class EnemySkillVisualReplica : MonoBehaviour
{
    // Executes is replica operation.
    public static bool IsReplica(Component component) =>
        component != null && component.GetComponentInParent<EnemySkillVisualReplica>() != null;
}
