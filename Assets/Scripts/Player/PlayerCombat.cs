using MysticJourney.API.Models.Response;
using System.Collections; // BẮT BUỘC THÊM DÒNG NÀY ĐỂ DÙNG COROUTINE
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("AoE Settings")]
    [SerializeField] private float maxCastRange = 6f;
    [SerializeField] private GameObject aoeIndicatorPrefab;

    [Header("Basic Attack Settings")]
    [SerializeField] private float attackCooldown = 0.5f;

    // 👇 THÊM BIẾN NÀY: Thời gian chờ trước khi mũi tên bay ra (giây)
    [SerializeField] private float basicAttackDelay = 0.2f;

    [SerializeField] private float basicAttackDamage = 25f;
    [SerializeField][Range(0f, 100f)] private float critRate = 20f; // 20% chí mạng
    [SerializeField] private float critDamageMultiplier = 1.5f; // x1.5 sát thương
    [Tooltip("KÉO PREFAB MŨI TÊN / CẦU PHÉP VÀO ĐÂY. NẾU LÀ ĐẤU SĨ CHÉM GẦN -> HÃY ĐỂ TRỐNG (NONE)")]
    [SerializeField] private GameObject basicAttackPrefab;

    [Header("Melee Fallback (Chỉ dùng khi basicAttackPrefab bị bỏ trống)")]
    [SerializeField] private float meleeRange = 1.2f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Skill Settings")]
    [SerializeField] private Transform firePoint;

    // 👇 THÊM BIẾN NÀY: Thời gian chờ trước khi tung Kỹ năng (giây)
    [SerializeField] private float skillCastDelay = 0.2f;

    [SerializeField] private GameObject skill1Prefab;
    [SerializeField] private GameObject skill2Prefab;
    [SerializeField] private GameObject skill3Prefab;

    [Header("Skills Cooldown")]
    [SerializeField] private float skill1Cooldown = 3f;
    [SerializeField] private float skill2Cooldown = 5f;
    [SerializeField] private float skill3Cooldown = 8f;

    private float nextAttackTime;
    private float nextSkill1Time;
    private float nextSkill2Time;
    private float nextSkill3Time;

    private Dictionary<int, float> _skillDamages = new Dictionary<int, float>();
    private Dictionary<int, float> _skillCorruptionCosts = new Dictionary<int, float>();
    private Dictionary<int, int> _skillIds = new Dictionary<int, int>();
    private Dictionary<int, float> _skillCooldowns = new Dictionary<int, float>();

    public static event System.Action<int, float> OnSkillCast;

    // --- AOE Aiming State ---
    private bool _isAimingAoE = false;
    private GameObject _aimingPrefab;
    private int _aimingSlotIndex;
    private float _aimingCooldown;
    private string _aimingAnimTrigger;
    private GameObject _aimingIndicatorInstance;
    private System.Action<float> _updateNextSkillTimeCallback;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();

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
                        critRate = response.CritRate * 100f; // API trả về 0.2 -> 20%
                        critDamageMultiplier = response.CritDamage;
                    }
                },
                error =>
                {
                    Debug.LogWarning($"[PlayerCombat] GetMyStats failed: {error.Message}");
                }
            );

            // Nạp kỹ năng cho Player và HUD lúc mới vào game (giống với cách tải Stats)
            var skillPanelMgr = FindFirstObjectByType<SkillPanelManager>(FindObjectsInactive.Include);
            if (skillPanelMgr != null)
            {
                skillPanelMgr.RefreshSkillList();
            }
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

        // Restore cooldown from server
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

                    // Tell UI to start cooldown visually
                    FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(s => s.slotIndex == slotIndex)
                        .ToList()
                        .ForEach(s => s.StartCooldown(remainingSeconds));
                }
            }
        }
    }

    #region Basic Attack Logic (ADAPTIVE)
    public void OnAttack(UnityEngine.InputSystem.InputValue value)
    {
        if (!value.isPressed) return;

        // Block attack if mouse is over UI (e.g. inventory, shop, HUD buttons)
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Attack();
    }

    private void Attack()
    {
        if (IsBusy() || Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + attackCooldown;
        animator.SetTrigger("Attack");

        // Bắt đầu đếm ngược thời gian delay trước khi bắn/chém
        StartCoroutine(ExecuteBasicAttackWithDelay(basicAttackDelay));
    }

    // 👇 HÀM CHỜ THỜI GIAN ĐÁNH THƯỜNG
    private IEnumerator ExecuteBasicAttackWithDelay(float delay)
    {
        // Chờ đúng số giây bạn đã thiết lập
        yield return new WaitForSeconds(delay);

        if (basicAttackPrefab != null)
        {
            SpawnBasicAttackProjectile();
        }
        else
        {
            PerformMeleeSweep();
        }
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

        // 👇 TẠO DANH SÁCH LỌC TRÙNG (Chỉ đánh mỗi con quái 1 lần trong 1 nhát chém)
        HashSet<EnemyEntity> damagedEnemies = new HashSet<EnemyEntity>();

        foreach (Collider2D enemyCollider in hitEnemies)
        {
            EnemyEntity enemy = enemyCollider.GetComponent<EnemyEntity>();

            // Nếu tìm thấy quái VÀ con quái này chưa bị chém trong nhát này
            if (enemy != null && !damagedEnemies.Contains(enemy))
            {
                damagedEnemies.Add(enemy); // Đánh dấu là đã chém trúng nó rồi

                // Tính toán chí mạng
                bool isCrit = Random.Range(0f, 100f) <= critRate;
                float finalDamage = basicAttackDamage;
                if (isCrit) finalDamage *= critDamageMultiplier;

                int damageInt = Mathf.RoundToInt(finalDamage);

                // Gây sát thương
                enemy.TakeDamage(damageInt);

                // Hiện số máu bay lên
                if (DamagePopupManager.Instance != null)
                {
                    DamagePopupManager.Instance.Create(enemy.transform.position, damageInt, isCrit, false);
                }
            }
        }
    }
    #endregion

    #region Skills Logic
    public void OnSkill1(InputValue value) { if (value.isPressed) TryCastSkill(skill1Prefab, 0, GetCooldown(0, skill1Cooldown), "Skill1"); }
    public void OnSkill2(InputValue value) { if (value.isPressed) TryCastSkill(skill2Prefab, 1, GetCooldown(1, skill2Cooldown), "Skill2"); }
    public void OnSkill3(InputValue value) { if (value.isPressed) TryCastSkill(skill3Prefab, 2, GetCooldown(2, skill3Cooldown), "Skill3"); }

    private float GetCooldown(int slotIndex, float fallback)
    {
        return _skillCooldowns.ContainsKey(slotIndex) ? _skillCooldowns[slotIndex] : fallback;
    }

    private void TryCastSkill(GameObject prefab, int slotIndex, float cooldown, string animTrigger)
    {
        Debug.Log($"[PlayerCombat] TryCastSkill slot={slotIndex}, prefab={(prefab != null ? prefab.name : "null")}, cooldown={cooldown}");
        if (prefab == null) return;

        float nextTime = slotIndex == 0 ? nextSkill1Time : slotIndex == 1 ? nextSkill2Time : nextSkill3Time;
        Debug.Log($"[PlayerCombat] IsBusy={IsBusy()}, Time.time={Time.time}, nextTime={nextTime}");
        if (IsBusy() || Time.time < nextTime) return;

        bool isAoE = prefab.GetComponent<SkillAoE>() != null;

        if (isAoE)
        {
            Debug.Log($"[PlayerCombat] Entering Aiming Mode for slot {slotIndex}");
            EnterAimingMode(prefab, slotIndex, cooldown, animTrigger);
        }
        else
        {
            Debug.Log($"[PlayerCombat] Executing Skill Confirmed for slot {slotIndex}");
            ExecuteSkillConfirmed(prefab, slotIndex, cooldown, animTrigger);
        }
    }

    private void ExecuteSkillConfirmed(GameObject prefab, int slotIndex, float cooldown, string animTrigger, Vector3? targetPosition = null)
    {
        // CHECK CORRUPTION LIMIT
        float corruptionCost = 0f;
        if (_skillCorruptionCosts.ContainsKey(slotIndex))
            corruptionCost = _skillCorruptionCosts[slotIndex];

        if (MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel + corruptionCost >= 100f)
        {
            Debug.LogWarning("Cannot cast skill! Corruption level would exceed 100.");
            // Trigger Game Over or Notify UI
            if (PlayerEntity.Instance != null)
                PlayerEntity.Instance.Die(); // Force die if corruption reaches 100
            return;
        }

        // Apply Corruption
        if (corruptionCost > 0)
        {
            MysticJourney.Core.Services.GameStateService.Instance.CorruptionLevel += corruptionCost;
            SyncCorruptionLevelToServer();
        }

        if (slotIndex == 0) nextSkill1Time = Time.time + cooldown;
        else if (slotIndex == 1) nextSkill2Time = Time.time + cooldown;
        else if (slotIndex == 2) nextSkill3Time = Time.time + cooldown;

        animator.SetTrigger(animTrigger);
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
        
        if (_aimingIndicatorInstance != null)
        {
            _aimingIndicatorInstance.SetActive(true);
        }
    }

    private void CancelAimingMode()
    {
        _isAimingAoE = false;
        if (_aimingIndicatorInstance != null)
        {
            _aimingIndicatorInstance.SetActive(false);
        }
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

    // 👇 HÀM CHỜ THỜI GIAN KỸ NĂNG
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
                spawnPosition = targetPosition.Value;
            }
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
                else
                {
                    spawnPosition = mouseWorldPosition;
                }
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
    #endregion

    private bool IsBusy() => animator.GetCurrentAnimatorStateInfo(0).IsName("BasicAttack") ||
                             animator.GetCurrentAnimatorStateInfo(0).IsName("SkillCast");

    private void OnDrawGizmosSelected()
    {
        if (firePoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(firePoint.position, meleeRange);
    }
}