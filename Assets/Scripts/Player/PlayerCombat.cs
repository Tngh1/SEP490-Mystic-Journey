using MysticJourney.API.Models.Response;
using System.Collections; // BẮT BUỘC THÊM DÒNG NÀY ĐỂ DÙNG COROUTINE
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("AoE Settings")]
    [SerializeField] private float maxCastRange = 6f;

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
    public static event System.Action<int, float> OnSkillCast;
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
    }

    #region Basic Attack Logic (ADAPTIVE)
    public void OnAttack(InputValue value)
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
    public void OnSkill1(InputValue value) => TryCastSkill(skill1Prefab, 0, ref nextSkill1Time, skill1Cooldown, "Skill1");
    public void OnSkill2(InputValue value) => TryCastSkill(skill2Prefab, 1, ref nextSkill2Time, skill2Cooldown, "Skill2");
    public void OnSkill3(InputValue value) => TryCastSkill(skill3Prefab, 2, ref nextSkill3Time, skill3Cooldown, "Skill3");

    private void TryCastSkill(GameObject prefab, int slotIndex, ref float nextTime, float cooldown, string animTrigger)
    {
        if (IsBusy() || Time.time < nextTime) return;

        nextTime = Time.time + cooldown;
        animator.SetTrigger(animTrigger);

        OnSkillCast?.Invoke(slotIndex, cooldown);
        // Bắt đầu đếm ngược thời gian delay trước khi tung Kỹ năng
        StartCoroutine(ExecuteSkillWithDelay(prefab, slotIndex, skillCastDelay));
    }

    // 👇 HÀM CHỜ THỜI GIAN KỸ NĂNG
    private IEnumerator ExecuteSkillWithDelay(GameObject prefab, int slotIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnSkill(prefab, slotIndex);
    }

    private void SpawnSkill(GameObject skillPrefab, int slotIndex)
    {
        if (skillPrefab == null || firePoint == null) return;

        bool isAoE = skillPrefab.GetComponent<SkillAoE>() != null;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (isAoE)
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