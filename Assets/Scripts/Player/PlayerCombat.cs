using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using MysticJourney.API.Models.Response;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Executes network behaviour operation.
public class PlayerCombat : NetworkBehaviour
{
    [Header("Animator / Aim")]
    [Tooltip("Animator that plays Attack / Skill1/2/3 triggers. If null, fetched via GetComponent.")]
    [SerializeField] private Animator animator;
    [FormerlySerializedAs("animation")]
    [SerializeField] private PlayerAnimation playerAnimation;

    [Header("AoE Settings")]
    [SerializeField] private float maxCastRange = 6f;
    [SerializeField] private GameObject aoeIndicatorPrefab;

    [Header("Basic Attack Settings")]
    [SerializeField] private float baseAttackCooldown = 0.4f;
    private float currentAttackCooldown;
    [SerializeField] private float basicAttackDelay = 0.2f;
    private float currentAttackDelay;
    [SerializeField] private float basicAttackDamage = 25f;
    [SerializeField, Range(0f, 100f)] private float critRate = 20f;
    [SerializeField] private float critDamageMultiplier = 1.5f;

    private float maxHp = 0f;
    private float def = 0f;
    private float attackSpeedStat = 100f;

    private float buffedDef = 0f;
    private float defBuffTimer = 0f;
    // Executes is debuff immune operation.
    public bool IsDebuffImmune { get; private set; } = false;
    private float debuffImmuneTimer = 0f;

    // Executes total def operation.
    public float TotalDef => def + buffedDef;
    // Executes total attack damage operation.
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

    private System.Collections.Generic.List<SpriteRenderer> _highlightedMonsters = new System.Collections.Generic.List<SpriteRenderer>();

    private float nextAttackTime;
    private float nextSkill1Time;
    private float nextSkill2Time;
    private float nextSkill3Time;

    private Dictionary<int, float> _skillDamages = new Dictionary<int, float>();
    private Dictionary<int, float> _skillCorruptionCosts = new Dictionary<int, float>();
    private Dictionary<int, int> _skillIds = new Dictionary<int, int>();
    private Dictionary<int, float> _skillCooldowns = new Dictionary<int, float>();
    private SkillData[] _skillMasterData;
    private bool _isLoadingEquippedSkills;

    public static event System.Action<int, float> OnSkillCast;

    private float _silenceTimer = 0f;
    // Executes is silenced operation.
    public bool IsSilenced => _silenceTimer > 0f;

    // Executes apply silence operation.
    public void ApplySilence(float duration, bool stackDuration = true, float maxCap = 5f)
    {
        if (stackDuration)
        {
            _silenceTimer = Mathf.Min(_silenceTimer + duration, maxCap);
        }
        else if (duration > _silenceTimer)
        {
            _silenceTimer = duration;
        }

        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null && _silenceTimer > 0f)
        {
            buffMgr.AddBuff("Silence", "silence_icon", _silenceTimer, true);
        }

        if (_isAimingAoE)
        {
            CancelAimingMode();
        }
    }

    private bool _isAimingAoE = false;
    private GameObject _aimingPrefab;
    private int _aimingSlotIndex;
    private float _aimingCooldown;
    private string _aimingAnimTrigger;
    private float _aimingStartTime;
    private GameObject _aimingIndicatorInstance;
    private GameObject _rangeIndicatorInstance;

    private GameplayInputProvider _input;


    // Initializes internal component caches and dependencies for PlayerCombat upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerAnimation == null) playerAnimation = GetComponent<PlayerAnimation>();
        currentAttackCooldown = baseAttackCooldown;
        currentAttackDelay = basicAttackDelay;

        _input = GetComponent<GameplayInputProvider>();
        if (_input == null) _input = gameObject.AddComponent<GameplayInputProvider>();

        if (firePoint == null)
        {
            Transform foundPoint = transform.Find("FirePoint");
            if (foundPoint != null) firePoint = foundPoint;
            else Debug.LogError($"[PlayerCombat] Không tìm thấy 'FirePoint' là con của {gameObject.name}!");
        }
    }

    // Executes set visual components operation.
    public void SetVisualComponents(Animator newAnimator, PlayerAnimation newAnimation)
    {
        animator = newAnimator;
        playerAnimation = newAnimation;
    }

    // Executes copy combat settings from operation.
    public void CopyCombatSettingsFrom(PlayerCombat source)
    {
        if (source == null) return;
        maxCastRange = source.maxCastRange;
        if (source.aoeIndicatorPrefab != null) aoeIndicatorPrefab = source.aoeIndicatorPrefab;
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

    // Performs startup initialization for PlayerCombat on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
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
                            float speedMultiplier = 100f / response.AttackSpeed;
                            currentAttackCooldown = speedMultiplier * baseAttackCooldown;
                            currentAttackDelay = Mathf.Max(0.2f, speedMultiplier * basicAttackDelay);
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

            if (GetComponent<NetworkPlayer>() == null)
            {
                LoadEquippedSkills();
            }
        }
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable() => SkillSlot.OnSkillEquipped += HandleSkillEquipped;
    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable() => SkillSlot.OnSkillEquipped -= HandleSkillEquipped;

    // Handles skill slot changes and syncs cooldown timers from server state.
    private void HandleSkillEquipped(int slotIndex, SkillData vData, PlayerSkillResponse sData)
    {
        if (Object != null && !Object.HasInputAuthority) return; // Only process skill equips on the locally controlled avatar
        if (vData == null || sData == null) return;

        if (slotIndex == 0) { skill1Prefab = vData.skillPrefab; skill1Cooldown = sData.CooldownSeconds; } // Slot 1 skill assignment
        else if (slotIndex == 1) { skill2Prefab = vData.skillPrefab; skill2Cooldown = sData.CooldownSeconds; } // Slot 2 skill assignment
        else if (slotIndex == 2) { skill3Prefab = vData.skillPrefab; skill3Cooldown = sData.CooldownSeconds; } // Slot 3 skill assignment

        _skillDamages[slotIndex] = (float)sData.EffectiveDamage; // Cache calculated skill damage
        _skillCorruptionCosts[slotIndex] = sData.CorruptionCost; // Cache corruption/mana cost
        _skillIds[slotIndex] = sData.PlayerSkillId; // Store database skill ID
        _skillCooldowns[slotIndex] = (float)sData.CooldownSeconds; // Store baseline cooldown in seconds

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
                    float remainingSeconds = (float)(nextTime - now).TotalSeconds; // Calculate remaining cooldown from server timestamp
                    if (slotIndex == 0) nextSkill1Time = Time.time + remainingSeconds;
                    else if (slotIndex == 1) nextSkill2Time = Time.time + remainingSeconds;
                    else if (slotIndex == 2) nextSkill3Time = Time.time + remainingSeconds;

                    FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(s => s.slotIndex == slotIndex)
                        .ToList()
                        .ForEach(s => s.StartCooldown(remainingSeconds)); // Sync UI cooldown sweep indicator
                }
            }
        }
    }

    // Executes request cast skill by slot operation.
    public void RequestCastSkillBySlot(int slotIndex)
    {
        if (slotIndex == 0) TryCastSkill(skill1Prefab, 0, GetCooldown(0, skill1Cooldown), "Skill1");
        else if (slotIndex == 1) TryCastSkill(skill2Prefab, 1, GetCooldown(1, skill2Cooldown), "Skill2");
        else if (slotIndex == 2) TryCastSkill(skill3Prefab, 2, GetCooldown(2, skill3Cooldown), "Skill3");
    }


    // Dispatches a basic attack command directed at the specified world aim point.
    public void RequestAttack(Vector2 aimWorldPosition)
    {
        if (_isAimingAoE || IsSilenced || IsBusy() || Time.time < nextAttackTime) return; // Prevent attack during cooldown, silence, or busy animation

        if (Runner == null || !Runner.IsRunning)
        {
            Attack(); // Local offline fallback attack execution
            return;
        }

        if (playerAnimation != null) playerAnimation.TriggerAttack(); // Play basic attack animation trigger on local character

        RPC_Attack(aimWorldPosition); // Dispatch networked attack RPC to state authority peer
    }

    // Routes a skill activation request by slot index to either instant cast or AoE targeting.
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

        TryCastSkill(prefab, slotIndex, cooldown, animTrigger); // Check cooldown/corruption and execute or enter aiming mode
    }


    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    // Executes rpc_attack operation.
    private void RPC_Attack(Vector2 aimWorldPosition)
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + currentAttackCooldown;

        RPC_PlayAttackAnim();

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(ExecuteBasicAttackWithDelay(currentAttackDelay));
    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_play attack anim operation.
    private void RPC_PlayAttackAnim()
    {
        if (playerAnimation != null) playerAnimation.TriggerAttack();
        else if (animator != null) animator.SetTrigger("Attack");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_play skill anim operation.
    private void RPC_PlaySkillAnim(int slotIndex)
    {
        if (playerAnimation != null) playerAnimation.TriggerSkill(slotIndex);
        else
        {
            string trigger = slotIndex == 0 ? "Skill1" : slotIndex == 1 ? "Skill2" : "Skill3";
            if (animator != null) animator.SetTrigger(trigger);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Process rpc spawn legacy skill visual using prefab name, position, rotation, and target position; it loads loaded skill prefab, instantiates the required Unity object, and updates active and guards invalid or unavailable states.
    private void RPC_SpawnLegacySkillVisual(string prefabName, Vector3 position, Quaternion rotation,
        Vector3 targetPosition, NetworkBool hasTargetPosition)
    {
        if (HasStateAuthority) return;

        GameObject prefab = FindLoadedSkillPrefab(prefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerCombat] Cannot resolve visual prefab '{prefabName}' on proxy.");
            return;
        }

        GameObject skillObj = Instantiate(prefab, position, rotation);
        skillObj.SetActive(false);
        PlayerSkillVisualReplica.Mark(skillObj, transform);
        ConfigureLegacySkill(skillObj, 0f, hasTargetPosition ? targetPosition : (Vector3?)null);
        ScheduleLegacySkillFallbackDestruction(skillObj);
        skillObj.SetActive(true);
    }

    // Executes find loaded skill prefab operation.
    // Validates input parameters against null or empty values.
    public static GameObject FindLoadedSkillPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        foreach (var skill in Resources.FindObjectsOfTypeAll<SkillData>())
        {
            if (skill != null && skill.skillPrefab != null && skill.skillPrefab.name == prefabName)
                return skill.skillPrefab;
        }

        return null;
    }


    // Executes is networked operation.
    private bool IsNetworked => Runner != null && Runner.IsRunning;

    private bool isPointerOverUI = false;

    // Executes on attack operation.
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

    // Executes on skill1 operation.
    public void OnSkill1(InputValue value) { if (!IsNetworked && value.isPressed) TryCastSkill(skill1Prefab, 0, GetCooldown(0, skill1Cooldown), "Skill1"); }
    // Executes on skill2 operation.
    public void OnSkill2(InputValue value) { if (!IsNetworked && value.isPressed) TryCastSkill(skill2Prefab, 1, GetCooldown(1, skill2Cooldown), "Skill2"); }
    // Executes on skill3 operation.
    public void OnSkill3(InputValue value) { if (!IsNetworked && value.isPressed) TryCastSkill(skill3Prefab, 2, GetCooldown(2, skill3Cooldown), "Skill3"); }


    // Executes attack operation.
    private void Attack()
    {
        if (_isAimingAoE || IsSilenced || IsBusy() || Time.time < nextAttackTime)
        {
            Debug.Log($"[PlayerCombat] Attack ignored. IsAimingAoE: {_isAimingAoE}, IsSilenced: {IsSilenced}, IsBusy: {IsBusy()}, Cooldown remaining: {(nextAttackTime - Time.time):F2}s");
            return;
        }
        Debug.Log($"[PlayerCombat] Attack triggered. Cooldown: {currentAttackCooldown}, Delay: {currentAttackDelay}");
        nextAttackTime = Time.time + currentAttackCooldown;

        if (playerAnimation != null) playerAnimation.TriggerAttack();
        else if (animator != null) animator.SetTrigger("Attack");
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(ExecuteBasicAttackWithDelay(currentAttackDelay));
    }


    // Executes execute basic attack with delay operation.
    private IEnumerator ExecuteBasicAttackWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (basicAttackPrefab != null) SpawnBasicAttackProjectile();
        else PerformMeleeSweep();
    }

    // Executes get active fire point operation.
    private Transform GetActiveFirePoint(Vector2 direction)
    {
        if (firePoint == null) return transform;

        if (Mathf.Abs(firePoint.localPosition.x) > 0.001f)
        {
            float absX = Mathf.Abs(firePoint.localPosition.x);
            Vector3 pos = firePoint.localPosition;
            if (direction.x < -0.01f)
            {
                pos.x = -absX;
            }
            else if (direction.x > 0.01f)
            {
                pos.x = absX;
            }
            firePoint.localPosition = pos;
        }

        return firePoint;
    }

    // Executes spawn basic attack projectile operation.
    private void SpawnBasicAttackProjectile()
    {
        PlayerMovement pm = GetComponent<PlayerMovement>();
        Vector2 direction = pm != null ? pm.LastMove : Vector2.right;
        if (direction == Vector2.zero) direction = Vector2.right;

        Transform spawnPoint = GetActiveFirePoint(direction);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0, 0, angle);

        if (IsNetworked && basicAttackPrefab != null &&
            basicAttackPrefab.GetComponent<NetworkObject>() != null)
        {
            float dmg = GetClassScaledDamage(basicAttackDamage);
            // Spawn through Fusion so state authority and replication are assigned consistently.
            Runner.Spawn(basicAttackPrefab, spawnPoint.position, rotation, Object.InputAuthority,
                (r, o) =>
                {
                    var np = o.GetComponent<NetworkSkillProjectile>();
                    if (np != null) np.Configure(dmg, 0f);
                });
            return;
        }

        GameObject projectileObj = Instantiate(basicAttackPrefab, spawnPoint.position, rotation);

        SkillProjectile projectileScript = projectileObj.GetComponent<SkillProjectile>();
        if (projectileScript != null)
        {
            projectileScript.Setup(GetClassScaledDamage(basicAttackDamage));
        }
    }

    // Executes perform melee sweep operation.
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
                // Randomize the eligible candidates before selecting this gameplay result.
                bool isCrit = Random.Range(0f, 100f) <= critRate;
                float finalDamage = GetClassScaledDamage(basicAttackDamage);
                if (isCrit) finalDamage *= critDamageMultiplier;
                int damageInt = Mathf.RoundToInt(finalDamage);
                enemy.TakeDamage(damageInt);

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


    // Executes get cooldown operation.
    private float GetCooldown(int slotIndex, float fallback)
    {
        float baseCd = _skillCooldowns.ContainsKey(slotIndex) ? _skillCooldowns[slotIndex] : fallback;
        string pClass = MysticJourney.Core.Services.GameStateService.Instance.PlayerClass;

        if (string.Equals(pClass, "Mage", System.StringComparison.OrdinalIgnoreCase))
        {
            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            float reduction = Mathf.Clamp((basicAttackDamage / 10f) * 0.02f, 0f, 0.5f);
            return baseCd * (1f - reduction);
        }
        else if (string.Equals(pClass, "Archer", System.StringComparison.OrdinalIgnoreCase))
        {
            float attackSpeedBonus = (attackSpeedStat - 100f) / 100f * 0.2f;
            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            float reduction = Mathf.Clamp(attackSpeedBonus, 0f, 0.4f);
            return baseCd * (1f - reduction);
        }
        else if (string.Equals(pClass, "Knight", System.StringComparison.OrdinalIgnoreCase))
        {
            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            float reduction = Mathf.Clamp((def / 50f) * 0.05f, 0f, 0.2f);
            return baseCd * (1f - reduction);
        }

        return baseCd;
    }

    // Calculate class-scaled attack damage: Mage uses 1.5x attack, Archer uses 1.2x, and Knight uses 1.0x attack plus 5% of maximum HP.
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
        else
        {
            float hpBonus = maxHp * 0.05f;
            return baseDamage + (basicAttackDamage * 1.0f) + hpBonus;
        }
    }

    // Return true when the prefab uses a targeted area or healing component, or matches one of the legacy targeted-skill prefab names.
    private bool IsTargetedAoESkill(GameObject prefab)
    {
        if (prefab == null) return false;
        return prefab.GetComponent<SkillAoE>() != null ||
               prefab.name.Contains("Lightsaber") ||
               prefab.name.Contains("FrozenSash") ||
               prefab.GetComponent<FrozenSashSkill>() != null ||
               prefab.name.Contains("PumpkinMagic") ||
               prefab.GetComponent<PumpkinMagicSkill>() != null ||
               prefab.name.Contains("PumpkinThrow") ||
               prefab.GetComponent<PumpkinThrowSkill>() != null ||
               prefab.GetComponent<NetworkSkillHealing>() != null;
    }

    // Reject missing, silenced, busy, or cooling-down casts, then enter area targeting or execute the selected skill immediately.
    private void TryCastSkill(GameObject prefab, int slotIndex, float cooldown, string animTrigger)
    {
        if (prefab == null) return;

        if (_isAimingAoE)
        {
            CancelAimingMode();
            return;
        }

        float nextTime = slotIndex == 0 ? nextSkill1Time : slotIndex == 1 ? nextSkill2Time : nextSkill3Time;
        if (IsSilenced || IsBusy() || Time.time < nextTime) return;

        bool isAoE = IsTargetedAoESkill(prefab);
        if (isAoE) EnterAimingMode(prefab, slotIndex, cooldown, animTrigger);
        else ExecuteSkillConfirmed(prefab, slotIndex, cooldown, animTrigger);
    }

    // Validate the corruption limit, consume the skill cost, start cooldown and animation state, then spawn or apply the confirmed skill effect.
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
            ApplyCorruptionDelta(corruptionCost);

        if (slotIndex == 0) nextSkill1Time = Time.time + cooldown;
        else if (slotIndex == 1) nextSkill2Time = Time.time + cooldown;
        else if (slotIndex == 2) nextSkill3Time = Time.time + cooldown;

        if (IsNetworked)
        {
            RPC_PlaySkillAnim(slotIndex);
        }

        if (playerAnimation != null) playerAnimation.TriggerSkill(slotIndex);
        else if (animator != null) animator.SetTrigger(animTrigger);
        OnSkillCast?.Invoke(slotIndex, cooldown);

        if (_skillIds.ContainsKey(slotIndex))
        {
            MysticJourney.API.Endpoints.SkillApi.Instance.RecordSkillCast(_skillIds[slotIndex]);
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(ExecuteSkillWithDelay(prefab, slotIndex, skillCastDelay, targetPosition));
    }

    // Store the pending skill cast, create and show area/range indicators, draw the cast radius, and update the target marker until the player confirms or cancels.
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

        if (_rangeIndicatorInstance == null)
        {
            _rangeIndicatorInstance = new GameObject("AimingRangeIndicator");
            _rangeIndicatorInstance.transform.SetParent(transform);
            _rangeIndicatorInstance.transform.localPosition = Vector3.zero;

            var line = _rangeIndicatorInstance.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.positionCount = 51;
            line.loop = true;
            line.sortingOrder = 10;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material = new Material(shader);
                line.startColor = new Color(0f, 1f, 1f, 0.4f);
                line.endColor = new Color(0f, 1f, 1f, 0.4f);
            }

            float angle = 0f;
            for (int i = 0; i < 51; i++)
            {
                float x = Mathf.Cos(Mathf.Deg2Rad * angle) * maxCastRange;
                float y = Mathf.Sin(Mathf.Deg2Rad * angle) * maxCastRange;
                line.SetPosition(i, new Vector3(x, y, 0f));
                angle += (360f / 50f);
            }
        }
        _rangeIndicatorInstance.SetActive(true);
    }

    // Exit targeting mode, hide the active indicators, and clear every pending prefab, slot, cooldown, and animation reference.
    private void CancelAimingMode()
    {
        _isAimingAoE = false;
        foreach (var sr in _highlightedMonsters)
        {
            if (sr != null) sr.color = Color.white;
        }
        _highlightedMonsters.Clear();
        if (_aimingIndicatorInstance != null) _aimingIndicatorInstance.SetActive(false);
        if (_rangeIndicatorInstance != null) _rangeIndicatorInstance.SetActive(false);
    }

    // Per-frame update loop for PlayerCombat.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_silenceTimer > 0f)
        {
            _silenceTimer -= Time.deltaTime;
        }
        if (animator != null && attackSpeedStat > 0)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("BasicAttack") || stateInfo.IsName("Attack"))
            {
                animator.speed = attackSpeedStat / 100f;
            }
            else
            {
                animator.speed = 1f;
            }
        }

        if (defBuffTimer > 0)
        {
            defBuffTimer -= Time.deltaTime;
            if (defBuffTimer <= 0) buffedDef = 0f;
        }

        if (debuffImmuneTimer > 0)
        {
            debuffImmuneTimer -= Time.deltaTime;
            if (debuffImmuneTimer <= 0)
            {
                IsDebuffImmune = false;
                var buffMgr = GetComponent<BuffManager>();
                if (buffMgr != null) buffMgr.IsStatusImmune = false;
            }
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

                Vector3 directionToMouse = mouseWorldPosition - transform.position;
                if (directionToMouse.magnitude > maxCastRange)
                {
                    mouseWorldPosition = transform.position + directionToMouse.normalized * maxCastRange;
                }

                _aimingIndicatorInstance.transform.position = mouseWorldPosition;

                if (_aimingPrefab != null)
                {
                    bool isTargetedSkill = _aimingPrefab.GetComponent<NetworkSkillHealing>() != null;
                    bool isHealingSkill = _aimingPrefab.GetComponent<NetworkSkillHealing>() != null;

                    if (isTargetedSkill)
                    {
                        foreach (var sr in _highlightedMonsters)
                        {
                            if (sr != null) sr.color = Color.white;
                        }
                        _highlightedMonsters.Clear();

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

                bool isTargetedSkill = _aimingPrefab != null && _aimingPrefab.GetComponent<NetworkSkillHealing>() != null;

                if (isTargetedSkill)
                {
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

                    foreach (var sr in _highlightedMonsters)
                    {
                        if (sr != null) sr.color = Color.white;
                    }
                    _highlightedMonsters.Clear();

                    if (selectedTarget == null)
                    {
                        if (_aimingPrefab != null && _aimingPrefab.GetComponent<NetworkSkillHealing>() != null)
                        {
                            selectedTarget = transform;
                        }
                        else
                        {
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
                foreach (var sr in _highlightedMonsters)
                {
                    if (sr != null) sr.color = Color.white;
                }
                _highlightedMonsters.Clear();

                CancelAimingMode();
            }
        }
    }

    // Executes apply corruption delta operation.
    public void ApplyCorruptionDelta(float delta)
    {
        var state = MysticJourney.Core.Services.GameStateService.Instance;
        if (state == null || Mathf.Approximately(delta, 0f)) return;
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        state.CorruptionLevel = Mathf.Clamp(state.CorruptionLevel + delta, 0f, 100f);
        PlayerHUDUIManager.Instance?.ApplyCorruption(state.CorruptionLevel);
        SyncCorruptionLevelToServer();
    }

    // Executes sync corruption level to server operation.
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

    // Executes add def buff operation.
    public void AddDefBuff(float amount, float duration)
    {
        if (amount > buffedDef) buffedDef = amount;
        if (duration > defBuffTimer) defBuffTimer = duration;

        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null) buffMgr.AddBuff("Protection", "shield_icon", duration, false);
    }

    // Executes add debuff immunity operation.
    public void AddDebuffImmunity(float duration)
    {
        IsDebuffImmune = true;
        if (duration > debuffImmuneTimer) debuffImmuneTimer = duration;

        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.SetMoveSpeedOverride(0f);
        _silenceTimer = 0f;

        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null)
        {
            buffMgr.IsStatusImmune = true;
            buffMgr.ClearAllDebuffs();
            buffMgr.AddBuff("Status Immunity", "immunity_icon", duration, false);
        }

        var burn = GetComponent<BurnDebuff>();
        if (burn != null) Destroy(burn);

        var slime = GetComponent<SlimeDebuff>();
        if (slime != null) Destroy(slime);

        var curse = GetComponentInChildren<DarknessCurseSkill>();
        if (curse != null) Destroy(curse.gameObject);
    }

    // Executes execute skill with delay operation.
    private IEnumerator ExecuteSkillWithDelay(GameObject prefab, int slotIndex, float delay, Vector3? targetPosition = null)
    {
        yield return new WaitForSeconds(delay);
        SpawnSkill(prefab, slotIndex, targetPosition);
    }

    // Executes spawn skill operation.
    private void SpawnSkill(GameObject skillPrefab, int slotIndex, Vector3? targetPosition = null)
    {
        if (skillPrefab == null || firePoint == null) return;

        bool isAoE = IsTargetedAoESkill(skillPrefab);
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
            PlayerMovement pm = GetComponent<PlayerMovement>();
            Vector2 direction = pm != null ? pm.LastMove : Vector2.right;
            if (direction == Vector2.zero) direction = Vector2.right;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            spawnRotation = Quaternion.Euler(0, 0, angle);
        }

        if (IsNetworked && skillPrefab.GetComponent<NetworkObject>() != null)
        {
            float netDamage = _skillDamages.ContainsKey(slotIndex) ? GetClassScaledDamage(_skillDamages[slotIndex]) : 0f;
            // Spawn through Fusion so state authority and replication are assigned consistently.
            Runner.Spawn(skillPrefab, spawnPosition, spawnRotation, Object.InputAuthority,
                (r, o) =>
                {
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

        if (IsNetworked && skillPrefab.GetComponent<ProtectiveShieldSkill>() == null)
        {
            RPC_SpawnLegacySkillVisual(
                skillPrefab.name,
                spawnPosition,
                spawnRotation,
                targetPosition ?? spawnPosition,
                targetPosition.HasValue);
        }

        GameObject skillObj = Instantiate(skillPrefab, spawnPosition, spawnRotation);

        if (_skillDamages.ContainsKey(slotIndex))
        {
            float damage = GetClassScaledDamage(_skillDamages[slotIndex]);
            ConfigureLegacySkill(skillObj, damage, targetPosition);
        }

        ScheduleLegacySkillFallbackDestruction(skillObj);
    }

    // Executes schedule legacy skill fallback destruction operation.
    private static void ScheduleLegacySkillFallbackDestruction(GameObject skillObj)
    {
        if (skillObj.GetComponent<LightsaberSkill>() == null &&
            skillObj.GetComponent<SkillAoE>() == null &&
            skillObj.GetComponent<SkillProjectile>() == null &&
            skillObj.GetComponent<PumpkinMagicSkill>() == null &&
            skillObj.GetComponent<PumpkinThrowSkill>() == null &&
            skillObj.GetComponent<ProtectiveShieldSkill>() == null)
        {
            Destroy(skillObj, 2f);
        }
    }

    // Executes load equipped skills operation.
    public void LoadEquippedSkills()
    {
        if (_isLoadingEquippedSkills) return;

        var skillApi = MysticJourney.API.Endpoints.SkillApi.Instance;
        if (skillApi == null)
        {
            Debug.LogWarning("[PlayerCombat] Skill API is unavailable; equipped skills were not loaded.");
            return;
        }

        SkillData[] masterData = ResolveSkillMasterData();
        if (masterData == null || masterData.Length == 0)
        {
            Debug.LogError("[PlayerCombat] No SkillData assets were found; equipped skills cannot be configured.");
            return;
        }

        _isLoadingEquippedSkills = true;
        skillApi.GetMySkills(
            onSuccess: response =>
            {
                _isLoadingEquippedSkills = false;
                if (this == null || response?.Skills == null) return;

                foreach (var playerSkill in response.Skills)
                {
                    if (!playerSkill.EquippedSlot.HasValue) continue;

                    int slotIndex = playerSkill.EquippedSlot.Value;
                    if (slotIndex < 0 || slotIndex > 2) continue;

                    SkillData visual = System.Array.Find(
                        masterData,
                        data => data != null && data.skillId == playerSkill.SkillId);

                    if (visual != null)
                    {
                        SkillSlot.BroadcastSkillEquipped(slotIndex, visual, playerSkill);
                    }
                }
            },
            onError: error =>
            {
                _isLoadingEquippedSkills = false;
                if (this != null)
                {
                    Debug.LogError($"[PlayerCombat] Failed to load equipped skills: {error.Message}");
                }
            });
    }

    // Executes resolve skill master data operation.
    private SkillData[] ResolveSkillMasterData()
    {
        if (_skillMasterData != null && _skillMasterData.Length > 0)
            return _skillMasterData;

        var hudManager = FindFirstObjectByType<HUDSkillManager>(FindObjectsInactive.Include);
        if (hudManager != null && hudManager.allSkillsInGame != null && hudManager.allSkillsInGame.Length > 0)
        {
            _skillMasterData = hudManager.allSkillsInGame;
            return _skillMasterData;
        }

        var panelManager = FindFirstObjectByType<SkillUIManager>(FindObjectsInactive.Include);
        if (panelManager != null && panelManager.allSkillsInGame != null && panelManager.allSkillsInGame.Length > 0)
        {
            _skillMasterData = panelManager.allSkillsInGame;
            return _skillMasterData;
        }

        _skillMasterData = Resources.LoadAll<SkillData>(string.Empty);
        return _skillMasterData;
    }

    // Executes configure legacy skill operation.
    private static void ConfigureLegacySkill(GameObject skillObj, float damage, Vector3? targetPosition)
    {
        var pumpkinThrow = skillObj.GetComponent<PumpkinThrowSkill>();
        if (pumpkinThrow != null)
        {
            if (targetPosition.HasValue) pumpkinThrow.Setup(damage, targetPosition.Value);
            else pumpkinThrow.Setup(damage);
            return;
        }

        var pumpkin = skillObj.GetComponent<PumpkinMagicSkill>();
        if (pumpkin != null) { pumpkin.Setup(damage); return; }

        var aoe = skillObj.GetComponent<SkillAoE>();
        if (aoe != null) { aoe.Setup(damage); return; }

        var frozenSash = skillObj.GetComponent<FrozenSashSkill>();
        if (frozenSash != null) { frozenSash.Setup(damage); return; }

        var lightsaber = skillObj.GetComponent<LightsaberSkill>();
        if (lightsaber != null) { lightsaber.Setup(damage); return; }

        var projectile = skillObj.GetComponent<SkillProjectile>();
        if (projectile != null) projectile.Setup(damage);
    }

    // Executes is busy operation.
    private bool IsBusy()
    {
        if (animator == null) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsName("SkillCast");
    }

    // Executes on draw gizmos selected operation.
    private void OnDrawGizmosSelected()
    {
        if (firePoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(firePoint.position, meleeRange);
    }
}
