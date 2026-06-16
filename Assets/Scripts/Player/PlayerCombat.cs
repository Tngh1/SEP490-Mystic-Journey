using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 0.5f;

    [Header("Skill Settings")]
    [SerializeField] private Transform firePoint;     // Vị trí skill sinh ra
    [SerializeField] private GameObject skill1Prefab; // Kéo Prefab skill 1 vào đây
    [SerializeField] private GameObject skill2Prefab; // Kéo Prefab skill 2 vào đây
    [SerializeField] private GameObject skill3Prefab; // Kéo Prefab skill 3 vào đây

    [Header("Skills Cooldown")]
    [SerializeField] private float skill1Cooldown = 3f;
    [SerializeField] private float skill2Cooldown = 5f;
    [SerializeField] private float skill3Cooldown = 8f;

    private float nextAttackTime;
    private float nextSkill1Time;
    private float nextSkill2Time;
    private float nextSkill3Time;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    #region Attack

    public void OnAttack(InputValue value)
    {
        if (!value.isPressed)
            return;

        Attack();
    }

    private void Attack()
    {
        if (IsBusy())
            return;

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        animator.SetTrigger("Attack");

        // Nếu có Prefab chém thường, bạn có thể Instantiate ở đây
    }

    #endregion

    #region Skill1

    public void OnSkill1(InputValue value)
    {
        if (!value.isPressed)
            return;

        if (IsBusy())
            return;

        if (Time.time < nextSkill1Time)
            return;

        nextSkill1Time = Time.time + skill1Cooldown;

        animator.SetTrigger("Skill1");

        // Gọi hàm tạo Prefab skill 1
        SpawnSkill(skill1Prefab);
    }

    #endregion

    #region Skill2

    public void OnSkill2(InputValue value)
    {
        if (!value.isPressed)
            return;

        if (IsBusy())
            return;

        if (Time.time < nextSkill2Time)
            return;

        nextSkill2Time = Time.time + skill2Cooldown;

        animator.SetTrigger("Skill2");

        // Gọi hàm tạo Prefab skill 2
        SpawnSkill(skill2Prefab);
    }

    #endregion

    #region Skill3

    public void OnSkill3(InputValue value)
    {
        if (!value.isPressed)
            return;

        if (IsBusy())
            return;

        if (Time.time < nextSkill3Time)
            return;

        nextSkill3Time = Time.time + skill3Cooldown;

        animator.SetTrigger("Skill3");

        // Gọi hàm tạo Prefab skill 3
        SpawnSkill(skill3Prefab);
    }

    #endregion

    // Hàm dùng chung để sinh ra Skill Prefab
    private void SpawnSkill(GameObject skillPrefab)
    {
        if (skillPrefab != null && firePoint != null)
        {
            Instantiate(skillPrefab, firePoint.position, firePoint.rotation);
        }
        else
        {
            Debug.LogWarning("Chưa gán Skill Prefab hoặc Fire Point trong Inspector!");
        }
    }

    private bool IsBusy()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        return state.IsName("BasicAttack") || state.IsName("SkillCast");
    }
}