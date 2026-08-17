using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
public class ExtraEnemySkillSpawner : MonoBehaviour
{
    // Executes extra skill data operation.
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

    // Initializes internal component caches and dependencies for ExtraEnemySkillSpawner upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _enemyBehaviour = GetComponent<EnemyBehaviour>();
        _enemyEntity = GetComponent<EnemyEntity>();
        _animator = GetComponent<Animator>();
        var networkEnemy = GetComponent<NetworkEnemy>();
        foreach (var skill in extraSkills)
            networkEnemy?.RegisterSkillPrefab(skill?.skillPrefab);
    }

    // Performs startup initialization for ExtraEnemySkillSpawner on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        foreach (var skill in extraSkills)
        {
            if (skill != null)
            {
                skill.nextCastTime = Time.time + 2f;
            }
        }
    }

    // Per-frame update loop for ExtraEnemySkillSpawner.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_enemyEntity != null && _enemyEntity.IsDead) return;

        Transform targetPlayer = FindPlayerTarget();
        if (targetPlayer == null) return;

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

    // Executes is in combat operation.
    // Evaluates conditions and returns a boolean result.
    private bool IsInCombat(Transform targetPlayer)
    {
        if (targetPlayer == null) return false;

        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        if (distance > combatDetectionRange) return false;

        return true;
    }

    // Executes cast extra skill operation.
    private void CastExtraSkill(ExtraSkillData skill, Transform targetPlayer)
    {
        GetComponent<NetworkEnemy>()?.NotifySkillAnimation();

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

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(SpawnSkillRoutine(skill, targetPlayer));
    }

    // Executes spawn skill routine operation.
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

        var networkEnemy = GetComponent<NetworkEnemy>();
        GameObject spawnedSkill = networkEnemy != null
            ? networkEnemy.SpawnEnemySkill(skill.skillPrefab, spawnPos, skill.spawnOnSelf)
            : Instantiate(skill.skillPrefab, spawnPos, Quaternion.identity);

        if (skill.spawnOnSelf && networkEnemy == null)
        {
            spawnedSkill.transform.SetParent(transform, true);
        }
    }

    // Executes find player target operation.
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
