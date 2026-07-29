using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script mở rộng kỹ năng cho Boss/Quái vật (Skill 3, Skill 4, Skill 5...).
/// Cho phép gán thêm nhiều kỹ năng mà không bị giới hạn bởi 2 ô mặc định trong EnemyBehaviour.
/// CHỈ THI TRIỂN SKILL KHI BOSS ĐANG TRONG TRẠNG THÁI GIAO TRANH VỚI PLAYER!
/// </summary>
public class ExtraEnemySkillSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ExtraSkillData
    {
        public string skillName = "Kỹ năng bổ sung";
        [Tooltip("Prefab của kỹ năng (VD: ResurrectionCocoon, FireWhirl,...)")]
        public GameObject skillPrefab;

        [Tooltip("Khoảng thời gian giữa mỗi lần dùng chiêu (giây)")]
        public float cooldown = 10f;

        [Tooltip("Thời gian delay chờ animation thi triển (giây)")]
        public float spawnDelay = 0.5f;

        [Tooltip("Tích chọn nếu skill xuất hiện ngay tại vị trí Boss (VD: Kén Phục Sinh, Giáp...). Bỏ tích nếu xuất hiện tại vị trí Player.")]
        public bool spawnOnSelf = true;

        [Tooltip("Có cho phép sử dụng kỹ năng này không")]
        public bool canCast = true;

        [HideInInspector] public float nextCastTime;
    }

    [Header("Extra Skills List")]
    [SerializeField] private List<ExtraSkillData> extraSkills = new List<ExtraSkillData>();

    [Header("Combat Settings")]
    [Tooltip("Phạm vi nhận diện giao tranh với Player (mét) - Chỉ thi triển skill khi Player trong phạm vi này")]
    [SerializeField] private float combatDetectionRange = 8.0f;

    [Header("Spawn Settings")]
    [Tooltip("Vị trí xuất hiện skill (nếu để None sẽ tự động chọn bản thân Boss hoặc vị trí Player)")]
    [SerializeField] private Transform spawnPoint;

    private EnemyBehaviour _enemyBehaviour;
    private EnemyEntity _enemyEntity;
    private Animator _animator;

    private void Awake()
    {
        _enemyBehaviour = GetComponent<EnemyBehaviour>();
        _enemyEntity = GetComponent<EnemyEntity>();
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Khởi tạo thời gian thi triển lần đầu cho các skill khi bắt đầu giao tranh
        foreach (var skill in extraSkills)
        {
            if (skill != null)
            {
                skill.nextCastTime = Time.time + 2f; // Lần đầu dùng skill sau 2s khi vào giao tranh
            }
        }
    }

    private void Update()
    {
        // Nếu Boss đã chết thì dừng thi triển skill
        if (_enemyEntity != null && _enemyEntity.IsDead) return;

        Transform targetPlayer = FindPlayerTarget();
        if (targetPlayer == null) return;

        // CHỈ THI TRIỂN SKILL KHI BOSS ĐANG TRONG TRẠNG THÁI GIAO TRANH
        if (!IsInCombat(targetPlayer)) return;

        foreach (var skill in extraSkills)
        {
            if (skill != null && skill.canCast && skill.skillPrefab != null)
            {
                if (Time.time >= skill.nextCastTime)
                {
                    skill.nextCastTime = Time.time + skill.cooldown;
                    CastExtraSkill(skill, targetPlayer);
                }
            }
        }
    }

    private bool IsInCombat(Transform targetPlayer)
    {
        if (targetPlayer == null) return false;

        // 1. Kiểm tra khoảng cách giữa Boss và Player
        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        if (distance > combatDetectionRange) return false;

        return true;
    }

    private void CastExtraSkill(ExtraSkillData skill, Transform targetPlayer)
    {
        // Kích hoạt animation dùng skill nếu có
        if (_animator != null)
        {
            if (_animator.HasParameter("CastSkill"))
            {
                _animator.SetTrigger("CastSkill");
            }
            else if (_animator.HasParameter("Attack"))
            {
                _animator.SetTrigger("Attack");
            }
        }

        StartCoroutine(SpawnSkillRoutine(skill, targetPlayer));
    }

    private IEnumerator SpawnSkillRoutine(ExtraSkillData skill, Transform targetPlayer)
    {
        if (skill.spawnDelay > 0)
        {
            yield return new WaitForSeconds(skill.spawnDelay);
        }

        if (_enemyEntity != null && _enemyEntity.IsDead) yield break;

        Vector3 spawnPos;
        if (skill.spawnOnSelf)
        {
            spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        }
        else
        {
            spawnPos = targetPlayer != null ? targetPlayer.position : transform.position;
        }

        GameObject spawnedSkill = Instantiate(skill.skillPrefab, spawnPos, Quaternion.identity);

        // Nếu là skill dạng kén bao bọc trên Boss thì tự gắn làm con của Boss (giữ nguyên vị trí spawnPos)
        if (skill.spawnOnSelf)
        {
            spawnedSkill.transform.SetParent(transform, true);
        }
    }

    private Transform FindPlayerTarget()
    {
        if (PlayerMovement.Instance != null)
        {
            return PlayerMovement.Instance.transform;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }
}
