using UnityEngine;
using System;

public class PlayerEntity : MonoBehaviour
{
    [SerializeField] private int maxHealth = 200;
    private int currentHealth;

    public static PlayerEntity Instance { get; private set; }

    public event EventHandler OnTakeHit;
    public event EventHandler OnDeath;

    // 👇 SỰ KIỆN TĨNH: Phát sóng mỗi khi máu thay đổi (Truyền đi Máu hiện tại và Máu tối đa)
    public static event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        Instance = this;
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // Khi nhân vật vừa xuất hiện, gửi ngay sự kiện để UI hiển thị mức máu đầy ban đầu (fallback)
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Lấy dữ liệu thật từ API
        if (MysticJourney.API.Core.ApiClient.Instance.HasToken())
        {
            MysticJourney.API.Endpoints.CharacterApi.Instance.GetMyStats(
                response =>
                {
                    if (response != null)
                    {
                        ApplyHealth(response.CurrentHp, response.MaxHp);
                    }
                },
                error =>
                {
                    Debug.LogWarning($"[PlayerEntity] GetMyStats failed: {error.Message}");
                }
            );
        }
    }

    public void ApplyHealth(int currentHp, int maxHp)
    {
        maxHealth = Mathf.Max(0, maxHp);
        currentHealth = maxHealth > 0 ? Mathf.Clamp(currentHp, 0, maxHealth) : Mathf.Max(0, currentHp);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private Coroutine syncHpCoroutine;

    public void TakeDamage(int damage)
    {
        bool isCrit = UnityEngine.Random.Range(0f, 100f) <= 10f;
        int finalDamage = isCrit ? Mathf.RoundToInt(damage * 1.5f) : damage;

        currentHealth -= finalDamage;
        if (currentHealth < 0) currentHealth = 0;

        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.Create(transform.position, finalDamage, isCrit, true);
        }

        // 👇 KHI BỊ ĐÁNH, GỌI SỰ KIỆN NÀY ĐỂ BÁO CHO UI CẬP NHẬT
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        OnTakeHit?.Invoke(this, EventArgs.Empty);

        if (currentHealth <= 0)
        {
            Die();
        }

        // --- ĐỒNG BỘ MÁU VỀ API (DEBOUNCE 1 GIÂY) ---
        if (syncHpCoroutine != null)
        {
            StopCoroutine(syncHpCoroutine);
        }
        syncHpCoroutine = StartCoroutine(SyncHpRoutine());
    }

    private System.Collections.IEnumerator SyncHpRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (MysticJourney.API.Core.ApiClient.Instance.HasToken())
        {
            MysticJourney.API.Endpoints.CharacterApi.Instance.UpdateHp(
                currentHealth,
                response => { /* Sync OK */ },
                error => { Debug.LogWarning($"[PlayerEntity] Sync HP failed: {error.Message}"); }
            );
        }
    }

    public void Die()
    {
        Debug.Log("Người chơi đã chết!");
        OnDeath?.Invoke(this, EventArgs.Empty);
    }
}