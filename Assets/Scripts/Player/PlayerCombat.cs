using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using MysticJourney.API.Models.Response;
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
    [SerializeField] private float baseAttackCooldown = 1.0f;
    private float currentAttackCooldown;
    [SerializeField] private float basicAttackDelay = 0.2f;
    [SerializeField] private float basicAttackDamage = 25f;
    [SerializeField, Range(0f, 100f)] private float critRate = 20f;
    [SerializeField] private float critDamageMultiplier = 1.5f;

    // Các chỉ số class-scaling
    private float maxHp = 0f;
    private float def = 0f;
    private float attackSpeedStat = 100f;

    // Buffs
    private float buffedDef = 0f;
    private float defBuffTimer = 0f;
    public bool IsDebuffImmune { get; private set; } = false;
    private float debuffImmuneTimer = 0f;

    public float TotalDef => def + buffedDef;
    public float TotalAttackDamage => basicAttackDamage;

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
    private System.Collections.Generic.List<SpriteRenderer> _highlightedMonsters = new System.Collections.Generic.List<SpriteRenderer>();

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
    private float _aimingStartTime;
    private GameObject _aimingIndicatorInstance;

    // Single source of truth for input. AoE aim position + confirm/cancel are
    // read from here instead of Mouse.current directly, keeping all input reads
    // centralised (SRP) and free of hardcoded devices.
    private GameplayInputProvider _input;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animation == null) animation = GetComponent<PlayerAnimation>();
        currentAttackCooldown = baseAttackCooldown;

        // Resolve (or add) the shared input provider on this GameObject.
        _input = GetComponent<GameplayInputProvider>();
        if (_input == null) _input = gameObject.AddComponent<GameplayInputProvider>();

        if (firePoint == null)
        {
            Transform foundPoint = transform.Find("FirePoint");
            if (foundPoint != null) firePoint = foundPoint;
            else Debug.LogError($"[PlayerCombat] Không tìm thấy 'FirePoint' là con của {gameObject.name}!");
        }
    }

    public void SetVisualComponents(Animator newAnimator, PlayerAnimation newAnimation)
    {
        animator = newAnimator;
        animation = newAnimation;
    }

    public void CopyCombatSettingsFrom(PlayerCombat source)
    {
        if (source == null) return;
        baseAttackCooldown = source.baseAttackCooldown;
        basicAttackDelay = source.basicAttackDelay;
        basicAttackDamage = source.basicAttackDamage;
        critRate = source.critRate;
        critDamageMultiplier = source.critDamageMultiplier;
        basicAttackPrefab = source.basicAttackPrefab;
        meleeRange = source.meleeRange;
        skillCastDelay = source.skillCastDelay;
        skill1Prefab = source.skill1Prefab;
        skill2Prefab = source.skill2Prefab;
        skill3Prefab = source.skill3Prefab;
        skill1Cooldown = source.skill1Cooldown;
        skill2Cooldown = source.skill2Cooldown;
        skill3Cooldown = source.skill3Cooldown;
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
                        maxHp = response.MaxHp;
                        def = response.Def;
                        attackSpeedStat = response.AttackSpeed;

                        if (response.AttackSpeed > 0)
                        {
                            currentAttackCooldown = (100f / response.AttackSpeed) * baseAttackCooldown;
                        }
                        
                        var buffMgr = GetComponent<BuffManager>();
                        if (buffMgr != null)
                        {
                            buffMgr.LoadFromServer(response.ActiveBuffs);
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
        if (IsBusy() || Time.time < nextAttackTime) return;

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

        TryCastSkill(prefab, slotIndex, cooldown, animTrigger);
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
    // Legacy single-player path — Unity Input System SendMessage callbacks.
    // These fire from PlayerInput (which reads the rebindable InputActions) and
    // are the SOLE combat-input path when Photon is NOT running. Under Fusion the
    // networked path (LocalInputCollector → NetworkInputData → RequestAttack) owns
    // combat input, so these are gated to offline-only to avoid a double-fire
    // (once locally here, once via RPC) — keeping "attack reading in one place".
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsNetworked => Runner != null && Runner.IsRunning;

    private bool isPointerOverUI = false;

    public void OnAttack(InputValue value)
    {
        if (IsNetworked) return;
        if (!value.isPressed) return;
        if (isPointerOverUI)
        {
            return;
        }
        Attack();
    }

    public void OnSkill1(InputValue value) { if (!IsNetworked && value.isPressed) TryCastSkill(skill1Prefab, 0, GetCooldown(0, skill1Cooldown), "Skill1"); }
    public void OnSkill2(InputValue value) { if (!IsNetworked && value.isPressed) TryCastSkill(skill2Prefab, 1, GetCooldown(1, skill2Cooldown), "Skill2"); }
    public void OnSkill3(InputValue value) { if (!IsNetworked && value.isPressed) TryCastSkill(skill3Prefab, 2, GetCooldown(2, skill3Cooldown), "Skill3"); }

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

        // Online: spawn a networked projectile so every client sees it fly and
        // damage is resolved on the enemy's authority. Only the caster (who owns
        // input authority and thus becomes the projectile's state authority in
        // Shared Mode) spawns it — Fusion replicates it to everyone else.
        if (IsNetworked && basicAttackPrefab != null &&
            basicAttackPrefab.GetComponent<NetworkObject>() != null)
        {
            float dmg = GetClassScaledDamage(basicAttackDamage);
            Runner.Spawn(basicAttackPrefab, firePoint.position, rotation, Object.InputAuthority,
                (r, o) =>
                {
                    var np = o.GetComponent<NetworkSkillProjectile>();
                    if (np != null) np.Configure(dmg, 0f);
                });
            return;
        }

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
            projectileScript.Setup(GetClassScaledDamage(basicAttackDamage));
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
                float finalDamage = GetClassScaledDamage(basicAttackDamage);
                if (isCrit) finalDamage *= critDamageMultiplier;
                int damageInt = Mathf.RoundToInt(finalDamage);
                enemy.TakeDamage(damageInt);

                // Damage number: online, broadcast via the enemy's NetworkEnemy so it
                // shows on EVERY client (melee has no networked object of its own to
                // broadcast from). Offline, spawn it locally as before.
                var net = enemy.Network;
                if (net != null)
                {
                    net.RPC_ShowDamagePopup(enemy.transform.position, damageInt, isCrit);
                }
                else if (DamagePopupManager.Instance != null)
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
        float baseCd = _skillCooldowns.ContainsKey(slotIndex) ? _skillCooldowns[slotIndex] : fallback;
        string pClass = MysticJourney.Core.Services.GameStateService.Instance.PlayerClass;

        if (string.Equals(pClass, "Mage", System.StringComparison.OrdinalIgnoreCase))
        {
            // Pháp sư: Spam skill dựa trên Công phép
            // Ví dụ: Mỗi 10 công phép giảm 2% hồi chiêu, tối đa giảm 50%
            float reduction = Mathf.Clamp((basicAttackDamage / 10f) * 0.02f, 0f, 0.5f);
            return baseCd * (1f - reduction);
        }
        else if (string.Equals(pClass, "Archer", System.StringComparison.OrdinalIgnoreCase))
        {
            // AD: Dựa trên Công vật lý & tốc đánh
            float attackSpeedBonus = (attackSpeedStat - 100f) / 100f * 0.2f; // Mỗi 100 tốc đánh giảm 20%
            float reduction = Mathf.Clamp(attackSpeedBonus, 0f, 0.4f);
            return baseCd * (1f - reduction);
        }
        else if (string.Equals(pClass, "Knight", System.StringComparison.OrdinalIgnoreCase))
        {
            // Đấu sĩ: Dựa trên Công vật lý / Tank (thường hồi khá lâu)
            // Ví dụ: Không giảm nhiều, giữ nguyên hoặc giảm chút xíu nhờ def
            float reduction = Mathf.Clamp((def / 50f) * 0.05f, 0f, 0.2f);
            return baseCd * (1f - reduction);
        }

        return baseCd;
    }

    private float GetClassScaledDamage(float baseDamage)
    {
        string pClass = MysticJourney.Core.Services.GameStateService.Instance.PlayerClass;
        if (string.Equals(pClass, "Mage", System.StringComparison.OrdinalIgnoreCase))
        {
            return baseDamage + (basicAttackDamage * 1.5f);
        }
        else if (string.Equals(pClass, "Archer", System.StringComparison.OrdinalIgnoreCase))
        {
            return baseDamage + (basicAttackDamage * 1.2f);
        }
        else // Knight
        {
            return baseDamage + (basicAttackDamage * 1.0f);
        }
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

        if (IsNetworked)
        {
            RPC_PlaySkillAnim(slotIndex);
        }

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
        _aimingStartTime = Time.time;
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
        // Update Buff Timers
        if (defBuffTimer > 0)
        {
            defBuffTimer -= Time.deltaTime;
            if (defBuffTimer <= 0) buffedDef = 0f;
        }
        
        if (debuffImmuneTimer > 0)
        {
            debuffImmuneTimer -= Time.deltaTime;
            if (debuffImmuneTimer <= 0) IsDebuffImmune = false;
        }
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            isPointerOverUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        if (_isAimingAoE)
        {
            Vector3? aimWorld = _input != null && _input.PointerWorldPosition.HasValue
                ? (Vector3)_input.PointerWorldPosition.Value
                : (Vector3?)null;

            if (_aimingIndicatorInstance != null && aimWorld.HasValue)
            {
                Vector3 mouseWorldPosition = aimWorld.Value;
                mouseWorldPosition.z = 0f;

                _aimingIndicatorInstance.transform.position = mouseWorldPosition;

                // Targeted Aiming Logic
                if (_aimingPrefab != null)
                {
                    bool isTargetedSkill = _aimingPrefab.name.Contains("Lightsaber") || _aimingPrefab.GetComponent<NetworkSkillHealing>() != null;
                    bool isHealingSkill = _aimingPrefab.GetComponent<NetworkSkillHealing>() != null;

                    if (isTargetedSkill)
                    {
                        // Reset old highlights
                        foreach (var sr in _highlightedMonsters)
                        {
                            if (sr != null) sr.color = Color.white;
                        }
                        _highlightedMonsters.Clear();

                        // Find new targets in circle (e.g., radius 3f)
                        float aimRadius = 3f;
                        int layerMask = isHealingSkill ? LayerMask.GetMask("Player") : enemyLayer;
                        Color highlightColor = isHealingSkill ? Color.green : Color.red;

                        Collider2D[] hits = Physics2D.OverlapCircleAll(mouseWorldPosition, aimRadius, layerMask);
                        foreach (var hit in hits)
                        {
                            var sprite = hit.GetComponentInChildren<SpriteRenderer>();
                            if (sprite != null)
                            {
                                sprite.color = highlightColor;
                                _highlightedMonsters.Add(sprite);
                            }
                        }
                    }
                }
            }

            if (_input != null && _input.PointerConfirmPressed && Time.time > _aimingStartTime + 0.1f)
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Vector3? targetPos = _aimingIndicatorInstance != null ? _aimingIndicatorInstance.transform.position : (Vector3?)null;

                bool isTargetedSkill = _aimingPrefab != null && (_aimingPrefab.name.Contains("Lightsaber") || _aimingPrefab.GetComponent<NetworkSkillHealing>() != null);

                if (isTargetedSkill)
                {
                    // Confirm selection
                    Transform selectedTarget = null;
                    float minDistance = float.MaxValue;
                    Vector3 clickPos = aimWorld ?? transform.position;

                    foreach (var sr in _highlightedMonsters)
                    {
                        if (sr != null)
                        {
                            float dist = Vector3.Distance(clickPos, sr.transform.position);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                selectedTarget = sr.transform;
                            }
                        }
                    }

                    // Reset color
                    foreach (var sr in _highlightedMonsters)
                    {
                        if (sr != null) sr.color = Color.white;
                    }
                    _highlightedMonsters.Clear();

                    if (selectedTarget == null)
                    {
                        if (_aimingPrefab != null && _aimingPrefab.GetComponent<NetworkSkillHealing>() != null)
                        {
                            // Self-cast
                            selectedTarget = transform;
                        }
                        else
                        {
                            // Clicked outside or no target, cancel skill without cooldown
                            CancelAimingMode();
                            return;
                        }
                    }
                    else
                    {
                        targetPos = selectedTarget.position;
                    }
                }

                ExecuteSkillConfirmed(_aimingPrefab, _aimingSlotIndex, _aimingCooldown, _aimingAnimTrigger, targetPos);
                CancelAimingMode();
            }
            else if (_input != null && _input.PointerCancelPressed)
            {
                if (_aimingPrefab != null && _aimingPrefab.name.Contains("Lightsaber"))
                {
                    foreach (var sr in _highlightedMonsters)
                    {
                        if (sr != null) sr.color = Color.white;
                    }
                    _highlightedMonsters.Clear();
                }
                CancelAimingMode();
            }
        }
    }

    private void SyncCorruptionLevelToServer()
    {
        float newCorruption = MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel;

        int profileId = MysticJourney.Core.Services.GameStateService.Instance.PlayerProfileId;
        if (profileId <= 0)
        {
            profileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, 0);
        }

        if (profileId <= 0)
        {
            Debug.LogWarning("[PlayerCombat] Cannot sync corruption: Unknown profile ID.");
            return;
        }

        var request = new MysticJourney.API.Models.Request.UpdatePlayerProfileRequest
        {
            CorruptionLevel = newCorruption
        };
        MysticJourney.API.Endpoints.PlayerApi.Instance.UpdateProfile(profileId, request, null, null);
    }

    public void AddDefBuff(float amount, float duration)
    {
        if (amount > buffedDef) buffedDef = amount; // override with stronger buff
        if (duration > defBuffTimer) defBuffTimer = duration;
        
        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null) buffMgr.AddBuff("Bảo Hộ", "shield_icon", duration, false);
    }

    public void AddDebuffImmunity(float duration)
    {
        IsDebuffImmune = true;
        if (duration > debuffImmuneTimer) debuffImmuneTimer = duration;
        
        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null) buffMgr.AddBuff("Kháng Hiệu Ứng", "immunity_icon", duration, false);
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
            if (targetPosition.HasValue)
            {
                Vector3 directionToTarget = targetPosition.Value - transform.position;
                if (directionToTarget.magnitude > maxCastRange)
                {
                    spawnPosition = transform.position + directionToTarget.normalized * maxCastRange;
                }
                else spawnPosition = targetPosition.Value;
            }
            else
            {
                Vector3 mouseWorldPosition = _input != null && _input.PointerWorldPosition.HasValue
                    ? (Vector3)_input.PointerWorldPosition.Value
                    : transform.position;
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
            Vector3 mouseWorldPosition = _input != null && _input.PointerWorldPosition.HasValue
                ? (Vector3)_input.PointerWorldPosition.Value
                : transform.position;
            mouseWorldPosition.z = 0f;

            Vector2 direction = (mouseWorldPosition - firePoint.position).normalized;
            if (direction == Vector2.zero)
                direction = PlayerMovement.Instance != null ? PlayerMovement.Instance.LastMove : Vector2.right;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            spawnRotation = Quaternion.Euler(0, 0, angle);
        }

        // Online: spawn the skill as a networked object so every client sees the
        // projectile / AoE and damage resolves on the enemy's authority. Only the
        // caster spawns (Shared Mode makes it the state authority); Fusion
        // replicates to everyone else. Falls through to Instantiate when offline
        // or when the prefab has no NetworkObject registered.
        if (IsNetworked && skillPrefab.GetComponent<NetworkObject>() != null)
        {
            float netDamage = _skillDamages.ContainsKey(slotIndex) ? GetClassScaledDamage(_skillDamages[slotIndex]) : 0f;
            bool flip = !isAoE && transform.localScale.x < 0;
            Runner.Spawn(skillPrefab, spawnPosition, spawnRotation, Object.InputAuthority,
                (r, o) =>
                {
                    if (flip)
                    {
                        Vector3 s = o.transform.localScale;
                        s.x *= -1;
                        o.transform.localScale = s;
                    }
                    if (isAoE)
                    {
                        var aoe = o.GetComponent<NetworkSkillAoE>();
                        if (aoe != null) aoe.Configure(netDamage);
                    }
                    else
                    {
                        var proj = o.GetComponent<NetworkSkillProjectile>();
                        if (proj != null) proj.Configure(netDamage, 0f);
                    }
                });
            return;
        }

        GameObject skillObj = Instantiate(skillPrefab, spawnPosition, spawnRotation);

        if (_skillDamages.ContainsKey(slotIndex))
        {
            float damage = GetClassScaledDamage(_skillDamages[slotIndex]);
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

    private bool IsBusy()
    {
        if (animator == null) return false;
        // Cho phép ngắt BasicAttack để đánh tiếp hoặc dùng chiêu (combat mượt hơn)
        return animator.GetCurrentAnimatorStateInfo(0).IsName("SkillCast");
    }

    private void OnDrawGizmosSelected()
    {
        if (firePoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(firePoint.position, meleeRange);
    }
}