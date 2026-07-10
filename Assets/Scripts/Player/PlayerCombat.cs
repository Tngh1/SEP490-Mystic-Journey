using Fusion;
using MysticJourney.API.Models.Response;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Combat executor for the local player. Owns attack timing, skill cooldowns,
/// projectile spawning, and AoE aim mode.
///
/// Entry points:
///   - <see cref="OnAttack(InputValue)"/>, <see cref="OnSkill1"/>, <see cref="OnSkill2"/>,
///     <see cref="OnSkill3"/>: legacy single-player path driven by Unity Input System.
///   - <see cref="RequestAttack"/>, <see cref="RequestSkill"/>: multiplayer path called
///     by NetworkPlayer.FixedUpdateNetwork. These use the same internal methods
///     but additionally replicate the animation trigger via RPC so every client
///     sees the attack animation.
///
/// Authoritative damage flow (Shared Mode):
///   - Input authority client calls RequestAttack(aim).
///   - RPC fires from input authority to state authority.
///   - State authority validates cooldown, rolls damage (deterministic Random),
///     and calls enemy.TakeDamage via the enemy NetworkBehaviour (Phase 12).
///   - State authority broadcasts RPC_PlayAttackAnimation to all clients so
///     every client plays the attack animation locally.
///
/// Single-player fallback:
///   - If the NetworkRunner is not running (no Photon connection), the legacy
///     OnAttack/OnSkill callbacks still work and call Attack()/TryCastSkill()
///     directly without RPCs.
///
/// Projectiles / AoE spawn (Phase 12 TODO):
///   - Today, SpawnBasicAttackProjectile and SpawnSkill call Instantiate.
///     These will be replaced with Runner.Spawn on a NetworkPrefab in Phase 12
///     so projectiles sync to all clients. The aimWorldPosition parameter is
///     already plumbed through for that future work.
/// </summary>
public class PlayerCombat : NetworkBehaviour
{
    [Header("Animator / Aim")]
    [Tooltip("Animator that plays Attack / Skill1/2/3 triggers. If null, fetched via GetComponent.")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerAnimation animation; // Phase 6 wrapper; optional

    [Header("AoE Settings")]
    [SerializeField] private float maxCastRange = 6f;
    [SerializeField] private GameObject aoeIndicatorPrefab;

    [Header("Basic Attack Settings")]
    [SerializeField] private float baseAttackCooldown = 0.5f;
    private float currentAttackCooldown;
    [SerializeField] private float basicAttackDelay = 0.2f;
    [SerializeField] private float basicAttackDamage = 25f;
    [SerializeField, Range(0f, 100f)] private float critRate = 20f;
    [SerializeField] private float critDamageMultiplier = 1.5f;
    [Tooltip("KÉO PREFAB MŨI TÊN / CẦU PHÉP VÀO ĐÂY. NẾU LÀ ĐẤU SĨ CHÉM GẦN -> HÃY ĐỂ TRỐNG (NONE)")]
    [SerializeField] private GameObject basicAttackPrefab;

    [Header("Melee Fallback")]
    [SerializeField] private float meleeRange = 1.2f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Skill Settings")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private float skillCastDelay = 0.2f;
    [SerializeField] private GameObject skill1Prefab;
    [SerializeField] private GameObject skill2Prefab;
    [SerializeField] private GameObject skill3Prefab;

    [Header("Skills Cooldown")]
    [SerializeField] private float skill1Cooldown = 3f;
    [SerializeField] private float skill2Cooldown = 5f;
    [SerializeField] private float skill3Cooldown = 8f;

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime state
    // ─────────────────────────────────────────────────────────────────────────

    private float nextAttackTime;
    private float nextSkill1Time;
    private float nextSkill2Time;
    private float nextSkill3Time;

    private Dictionary<int, float> _skillDamages = new Dictionary<int, float>();
    private Dictionary<int, float> _skillCorruptionCosts = new Dictionary<int, float>();
    private Dictionary<int, int> _skillIds = new Dictionary<int, int>();
    private Dictionary<int, float> _skillCooldowns = new Dictionary<int, float>();

    public static event System.Action<int, float> OnSkillCast;

    // AoE aiming state (local-only — each client aims independently)
    private bool _isAimingAoE = false;
    private GameObject _aimingPrefab;
    private int _aimingSlotIndex;
    private float _aimingCooldown;
    private string _aimingAnimTrigger;
    private GameObject _aimingIndicatorInstance;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animation == null) animation = GetComponent<PlayerAnimation>();
        currentAttackCooldown = baseAttackCooldown;

        if (firePoint == null)
        {
            Transform foundPoint = transform.Find("FirePoint");
            if (foundPoint != null) firePoint = foundPoint;
            else Debug.LogError($"[PlayerCombat] Không tìm thấy 'FirePoint' là con của {gameObject.name}!");
        }
    }

    private void Start()
    {
        if (MysticJourney.API.Core.ApiClient.Instance.HasToken())
        {
            MysticJourney.API.Endpoints.CharacterApi.Instance.GetMyStats(
                response =>
                {
                    if (response != null)
                    {
                        basicAttackDamage = response.Atk;
                        critRate = response.CritRate;
                        critDamageMultiplier = response.CritDamage / 100f;
                        if (response.AttackSpeed > 0)
                        {
                            currentAttackCooldown = (100f / response.AttackSpeed) * baseAttackCooldown;
                        }
                    }
                },
                error => Debug.LogWarning($"[PlayerCombat] GetMyStats failed: {error.Message}")
            );

            var skillPanelMgr = FindFirstObjectByType<SkillPanelManager>(FindObjectsInactive.Include);
            if (skillPanelMgr != null) skillPanelMgr.RefreshSkillList();
        }
    }

    private void OnEnable() => SkillSlot.OnSkillEquipped += HandleSkillEquipped;
    private void OnDisable() => SkillSlot.OnSkillEquipped -= HandleSkillEquipped;

    private void HandleSkillEquipped(int slotIndex, SkillData vData, PlayerSkillResponse sData)
    {
        if (vData == null || sData == null) return;

        if (slotIndex == 0) { skill1Prefab = vData.skillPrefab; skill1Cooldown = sData.CooldownSeconds; }
        else if (slotIndex == 1) { skill2Prefab = vData.skillPrefab; skill2Cooldown = sData.CooldownSeconds; }
        else if (slotIndex == 2) { skill3Prefab = vData.skillPrefab; skill3Cooldown = sData.CooldownSeconds; }

        _skillDamages[slotIndex] = (float)sData.EffectiveDamage;
        _skillCorruptionCosts[slotIndex] = sData.CorruptionCost;
        _skillIds[slotIndex] = sData.PlayerSkillId;
        _skillCooldowns[slotIndex] = (float)sData.CooldownSeconds;

        if (!string.IsNullOrEmpty(sData.NextAvailableTime))
        {
            if (System.DateTime.TryParse(sData.NextAvailableTime,
                                         System.Globalization.CultureInfo.InvariantCulture,
                                         System.Globalization.DateTimeStyles.AdjustToUniversal,
                                         out System.DateTime nextTime))
            {
                var now = System.DateTime.UtcNow;
                if (nextTime > now)
                {
                    float remainingSeconds = (float)(nextTime - now).TotalSeconds;
                    if (slotIndex == 0) nextSkill1Time = Time.time + remainingSeconds;
                    else if (slotIndex == 1) nextSkill2Time = Time.time + remainingSeconds;
                    else if (slotIndex == 2) nextSkill3Time = Time.time + remainingSeconds;

                    FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(s => s.slotIndex == slotIndex)
                        .ToList()
                        .ForEach(s => s.StartCooldown(remainingSeconds));
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multiplayer entry points — called by NetworkPlayer.FixedUpdateNetwork
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Network-driven attack request. Validates the request on the state authority
    /// and broadcasts the animation trigger to all clients.
    /// </summary>
    public void RequestAttack(Vector2 aimWorldPosition)
    {
        if (Runner == null || !Runner.IsRunning)
        {
            // Single-player fallback: execute locally.
            Attack();
            return;
        }

        // Local client plays attack animation immediately for responsiveness,
        // server will validate cooldown and broadcast the authoritative trigger.
        if (animation != null) animation.TriggerAttack();

        RPC_Attack(aimWorldPosition);
    }

    /// <summary>
    /// Network-driven skill request. slotIndex = 0/1/2 for Skill1/2/3.
    /// </summary>
    public void RequestSkill(int slotIndex, Vector2 aimWorldPosition)
    {
        GameObject prefab;
        float cooldown;
        string animTrigger;
        switch (slotIndex)
        {
            case 0: prefab = skill1Prefab; cooldown = GetCooldown(0, skill1Cooldown); animTrigger = "Skill1"; break;
            case 1: prefab = skill2Prefab; cooldown = GetCooldown(1, skill2Cooldown); animTrigger = "Skill2"; break;
            case 2: prefab = skill3Prefab; cooldown = GetCooldown(2, skill3Cooldown); animTrigger = "Skill3"; break;
            default: return;
        }

        if (prefab == null) return;

        if (Runner == null || !Runner.IsRunning)
        {
            TryCastSkill(prefab, slotIndex, cooldown, animTrigger);
            return;
        }

        if (animation != null) animation.TriggerSkill(slotIndex);

        RPC_Skill(slotIndex, aimWorldPosition);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs — Input Authority → State Authority
    // ─────────────────────────────────────────────────────────────────────────

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Attack(Vector2 aimWorldPosition)
    {
        // Server-side validation: cooldown.
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + currentAttackCooldown;

        // Trigger animation on every client (defensive; local client already did it).
        RPC_PlayAttackAnim();

        // Execute the actual attack. In Phase 12 this will route through
        // a server-side damage pipeline with deterministic Random.
        StartCoroutine(ExecuteBasicAttackWithDelay(basicAttackDelay));
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Skill(int slotIndex, Vector2 aimWorldPosition)
    {
        GameObject prefab;
        float cooldown;
        string animTrigger;
        switch (slotIndex)
        {
            case 0: prefab = skill1Prefab; cooldown = GetCooldown(0, skill1Cooldown); animTrigger = "Skill1"; break;
            case 1: prefab = skill2Prefab; cooldown = GetCooldown(1, skill2Cooldown); animTrigger = "Skill2"; break;
            case 2: prefab = skill3Prefab; cooldown = GetCooldown(2, skill3Cooldown); animTrigger = "Skill3"; break;
            default: return;
        }
        if (prefab == null) return;

        if (IsBusy()) return;

        // Validate cooldown + corruption on server.
        float nextTime = slotIndex == 0 ? nextSkill1Time : slotIndex == 1 ? nextSkill2Time : nextSkill3Time;
        if (Time.time < nextTime) return;

        float corruptionCost = _skillCorruptionCosts.ContainsKey(slotIndex) ? _skillCorruptionCosts[slotIndex] : 0f;
        if (MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel + corruptionCost >= 100f)
        {
            if (MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel >= 100f)
            {
                if (TryGetComponent<PlayerEntity>(out var pe)) pe.Die();
            }
            return;
        }

        if (corruptionCost > 0)
        {
            MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel += corruptionCost;
            SyncCorruptionLevelToServer();
        }

        if (slotIndex == 0) nextSkill1Time = Time.time + cooldown;
        else if (slotIndex == 1) nextSkill2Time = Time.time + cooldown;
        else if (slotIndex == 2) nextSkill3Time = Time.time + cooldown;

        // Authoritative spawn of the skill prefab. Phase 12 will replace
        // Instantiate with Runner.Spawn on a NetworkPrefab.
        RPC_PlaySkillAnim(slotIndex);
        StartCoroutine(ExecuteSkillWithDelay(prefab, slotIndex, skillCastDelay, aimWorldPosition));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAttackAnim()
    {
        if (animation != null) animation.TriggerAttack();
        else if (animator != null) animator.SetTrigger("Attack");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlaySkillAnim(int slotIndex)
    {
        if (animation != null) animation.TriggerSkill(slotIndex);
        else
        {
            string trigger = slotIndex == 0 ? "Skill1" : slotIndex == 1 ? "Skill2" : "Skill3";
            if (animator != null) animator.SetTrigger(trigger);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Legacy single-player path — Unity Input System callbacks.
    // These continue to work when Photon is not running (Runner == null).
    // ─────────────────────────────────────────────────────────────────────────

    public void OnAttack(InputValue value)
    {
        if (!value.isPressed) return;
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }
        Attack();
    }

    public void OnSkill1(InputValue value) { if (value.isPressed) TryCastSkill(skill1Prefab, 0, GetCooldown(0, skill1Cooldown), "Skill1"); }
    public void OnSkill2(InputValue value) { if (value.isPressed) TryCastSkill(skill2Prefab, 1, GetCooldown(1, skill2Cooldown), "Skill2"); }
    public void OnSkill3(InputValue value) { if (value.isPressed) TryCastSkill(skill3Prefab, 2, GetCooldown(2, skill3Cooldown), "Skill3"); }

    // ─────────────────────────────────────────────────────────────────────────
    // Basic attack
    // ─────────────────────────────────────────────────────────────────────────

    private void Attack()
    {
        if (IsBusy() || Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + currentAttackCooldown;
        if (animation != null) animation.TriggerAttack();
        else if (animator != null) animator.SetTrigger("Attack");
        StartCoroutine(ExecuteBasicAttackWithDelay(basicAttackDelay));
    }

    private IEnumerator ExecuteBasicAttackWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (basicAttackPrefab != null) SpawnBasicAttackProjectile();
        else PerformMeleeSweep();
    }

    private void SpawnBasicAttackProjectile()
    {
        if (firePoint == null) return;

        Vector2 direction = PlayerMovement.Instance != null ? PlayerMovement.Instance.LastMove : Vector2.right;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0, 0, angle);

        GameObject projectileObj = Instantiate(basicAttackPrefab, firePoint.position, rotation);

        SkillProjectile projectileScript = projectileObj.GetComponent<SkillProjectile>();
        if (projectileScript != null)
        {
            if (transform.localScale.x < 0)
            {
                Vector3 scale = projectileObj.transform.localScale;
                scale.x *= -1;
                projectileObj.transform.localScale = scale;
            }
            projectileScript.Setup(basicAttackDamage);
        }
    }

    private void PerformMeleeSweep()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(firePoint.position, meleeRange, enemyLayer);
        HashSet<EnemyEntity> damagedEnemies = new HashSet<EnemyEntity>();
        foreach (Collider2D enemyCollider in hitEnemies)
        {
            EnemyEntity enemy = enemyCollider.GetComponent<EnemyEntity>();
            if (enemy != null && !damagedEnemies.Contains(enemy))
            {
                damagedEnemies.Add(enemy);
                bool isCrit = Random.Range(0f, 100f) <= critRate;
                float finalDamage = basicAttackDamage;
                if (isCrit) finalDamage *= critDamageMultiplier;
                int damageInt = Mathf.RoundToInt(finalDamage);
                enemy.TakeDamage(damageInt);
                if (DamagePopupManager.Instance != null)
                {
                    DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Skills
    // ─────────────────────────────────────────────────────────────────────────

    private float GetCooldown(int slotIndex, float fallback)
    {
        return _skillCooldowns.ContainsKey(slotIndex) ? _skillCooldowns[slotIndex] : fallback;
    }

    private void TryCastSkill(GameObject prefab, int slotIndex, float cooldown, string animTrigger)
    {
        if (prefab == null) return;

        float nextTime = slotIndex == 0 ? nextSkill1Time : slotIndex == 1 ? nextSkill2Time : nextSkill3Time;
        if (IsBusy() || Time.time < nextTime) return;

        bool isAoE = prefab.GetComponent<SkillAoE>() != null;
        if (isAoE) EnterAimingMode(prefab, slotIndex, cooldown, animTrigger);
        else ExecuteSkillConfirmed(prefab, slotIndex, cooldown, animTrigger);
    }

    private void ExecuteSkillConfirmed(GameObject prefab, int slotIndex, float cooldown, string animTrigger, Vector3? targetPosition = null)
    {
        float corruptionCost = _skillCorruptionCosts.ContainsKey(slotIndex) ? _skillCorruptionCosts[slotIndex] : 0f;
        if (MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel + corruptionCost >= 100f)
        {
            Debug.LogWarning("Cannot cast skill! Corruption level would exceed 100.");
            if (PlayerEntity.Instance != null) PlayerEntity.Instance.Die();
            return;
        }

        if (corruptionCost > 0)
        {
            MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel += corruptionCost;
            SyncCorruptionLevelToServer();
        }

        if (slotIndex == 0) nextSkill1Time = Time.time + cooldown;
        else if (slotIndex == 1) nextSkill2Time = Time.time + cooldown;
        else if (slotIndex == 2) nextSkill3Time = Time.time + cooldown;

        if (animation != null) animation.TriggerSkill(slotIndex);
        else if (animator != null) animator.SetTrigger(animTrigger);
        OnSkillCast?.Invoke(slotIndex, cooldown);

        if (_skillIds.ContainsKey(slotIndex))
        {
            MysticJourney.API.Endpoints.SkillApi.Instance.RecordSkillCast(_skillIds[slotIndex]);
        }

        StartCoroutine(ExecuteSkillWithDelay(prefab, slotIndex, skillCastDelay, targetPosition));
    }

    private void EnterAimingMode(GameObject prefab, int slotIndex, float cooldown, string animTrigger)
    {
        _isAimingAoE = true;
        _aimingPrefab = prefab;
        _aimingSlotIndex = slotIndex;
        _aimingCooldown = cooldown;
        _aimingAnimTrigger = animTrigger;

        if (aoeIndicatorPrefab != null && _aimingIndicatorInstance == null)
        {
            _aimingIndicatorInstance = Instantiate(aoeIndicatorPrefab);
        }
        if (_aimingIndicatorInstance != null) _aimingIndicatorInstance.SetActive(true);
    }

    private void CancelAimingMode()
    {
        _isAimingAoE = false;
        if (_aimingIndicatorInstance != null) _aimingIndicatorInstance.SetActive(false);
    }

    private void Update()
    {
        if (_isAimingAoE)
        {
            if (_aimingIndicatorInstance != null)
            {
                Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
                Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
                mouseWorldPosition.z = 0f;

                Vector3 directionToMouse = mouseWorldPosition - transform.position;
                if (directionToMouse.magnitude > maxCastRange)
                {
                    _aimingIndicatorInstance.transform.position = transform.position + directionToMouse.normalized * maxCastRange;
                }
                else
                {
                    _aimingIndicatorInstance.transform.position = mouseWorldPosition;
                }
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Vector3? targetPos = _aimingIndicatorInstance != null ? _aimingIndicatorInstance.transform.position : (Vector3?)null;
                ExecuteSkillConfirmed(_aimingPrefab, _aimingSlotIndex, _aimingCooldown, _aimingAnimTrigger, targetPos);
                CancelAimingMode();
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelAimingMode();
            }
        }
    }

    private void SyncCorruptionLevelToServer()
    {
        float newCorruption = MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel;
        var request = new MysticJourney.API.Models.Request.UpdatePlayerProfileRequest
        {
            CorruptionLevel = newCorruption
        };
        int profileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, 0);
        if (profileId > 0)
        {
            MysticJourney.API.Endpoints.PlayerApi.Instance.UpdateProfile(profileId, request, null, null);
        }
    }

    private IEnumerator ExecuteSkillWithDelay(GameObject prefab, int slotIndex, float delay, Vector3? targetPosition = null)
    {
        yield return new WaitForSeconds(delay);
        SpawnSkill(prefab, slotIndex, targetPosition);
    }

    private void SpawnSkill(GameObject skillPrefab, int slotIndex, Vector3? targetPosition = null)
    {
        if (skillPrefab == null || firePoint == null) return;

        bool isAoE = skillPrefab.GetComponent<SkillAoE>() != null;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (isAoE)
        {
            if (targetPosition.HasValue) spawnPosition = targetPosition.Value;
            else
            {
                Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
                Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
                mouseWorldPosition.z = 0f;

                Vector3 directionToMouse = mouseWorldPosition - transform.position;
                if (directionToMouse.magnitude > maxCastRange)
                {
                    spawnPosition = transform.position + directionToMouse.normalized * maxCastRange;
                }
                else spawnPosition = mouseWorldPosition;
            }
            spawnRotation = Quaternion.identity;
        }
        else
        {
            spawnPosition = firePoint.position;
            Vector2 direction = PlayerMovement.Instance != null ? PlayerMovement.Instance.LastMove : Vector2.right;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            spawnRotation = Quaternion.Euler(0, 0, angle);
        }

        GameObject skillObj = Instantiate(skillPrefab, spawnPosition, spawnRotation);

        if (_skillDamages.ContainsKey(slotIndex))
        {
            float damage = _skillDamages[slotIndex];
            if (isAoE)
            {
                skillObj.GetComponent<SkillAoE>().Setup(damage);
            }
            else
            {
                var projectile = skillObj.GetComponent<SkillProjectile>();
                if (projectile != null)
                {
                    if (transform.localScale.x < 0)
                    {
                        Vector3 scale = skillObj.transform.localScale;
                        scale.x *= -1;
                        skillObj.transform.localScale = scale;
                    }
                    projectile.Setup(damage);
                }
            }
        }
    }

    private bool IsBusy() => animator.GetCurrentAnimatorStateInfo(0).IsName("BasicAttack") ||
                             animator.GetCurrentAnimatorStateInfo(0).IsName("SkillCast");

    private void OnDrawGizmosSelected()
    {
        if (firePoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(firePoint.position, meleeRange);
    }
}