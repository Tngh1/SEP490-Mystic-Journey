using MysticJourney.API.Models.Response;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 0.5f;

    [Header("Skill Settings")]
    [SerializeField] private Transform firePoint;
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

    #region Skills Logic
    public void OnSkill1(InputValue value) => TryCastSkill(skill1Prefab, 0, ref nextSkill1Time, skill1Cooldown, "Skill1");
    public void OnSkill2(InputValue value) => TryCastSkill(skill2Prefab, 1, ref nextSkill2Time, skill2Cooldown, "Skill2");
    public void OnSkill3(InputValue value) => TryCastSkill(skill3Prefab, 2, ref nextSkill3Time, skill3Cooldown, "Skill3");

    private void TryCastSkill(GameObject prefab, int slotIndex, ref float nextTime, float cooldown, string animTrigger)
    {
        if (IsBusy() || Time.time < nextTime) return;

        nextTime = Time.time + cooldown;
        animator.SetTrigger(animTrigger);
        SpawnSkill(prefab, slotIndex);
    }

    private void SpawnSkill(GameObject skillPrefab, int slotIndex)
    {
        if (skillPrefab == null || firePoint == null) return;

        // 1. Lấy hướng từ PlayerMovement (Cần đảm bảo PlayerMovement.Instance tồn tại)
        Vector2 direction = PlayerMovement.Instance != null ? PlayerMovement.Instance.LastMove : Vector2.right;

        // 2. Tính góc quay từ hướng di chuyển
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0, 0, angle);

        // 3. Instantiate với hướng quay đã tính toán
        GameObject skillObj = Instantiate(skillPrefab, firePoint.position, rotation);

        // 4. Truyền damage
        var projectile = skillObj.GetComponent<SkillProjectile>();
        if (projectile != null && _skillDamages.ContainsKey(slotIndex))
        {
            projectile.Setup(_skillDamages[slotIndex]);
        }
    }
    #endregion

    private bool IsBusy() => animator.GetCurrentAnimatorStateInfo(0).IsName("BasicAttack") ||
                             animator.GetCurrentAnimatorStateInfo(0).IsName("SkillCast");
}