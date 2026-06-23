using UnityEngine;
using System;

public class PlayerEntity : MonoBehaviour
{
    [SerializeField] private int maxHealth = 200;
    private int currentHealth;

    // Dùng Singleton để Quái vật dễ dàng tìm thấy người chơi
    public static PlayerEntity Instance { get; private set; }

    public event EventHandler OnTakeHit;
    public event EventHandler OnDeath;

    private void Awake()
    {
        Instance = this;
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"[Player] Bị quái cào {damage} máu. Còn lại: {currentHealth}/{maxHealth}");

        OnTakeHit?.Invoke(this, EventArgs.Empty);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Người chơi đã chết!");
        OnDeath?.Invoke(this, EventArgs.Empty);
        // Ở đây sau này bạn có thể gọi màn hình Game Over hoặc Reset Level
    }
}