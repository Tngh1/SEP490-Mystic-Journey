using UnityEngine;

public class SlimeDebuff : MonoBehaviour
{
    private float slowMultiplier;
    private int damagePerTick;
    private float tickInterval;
    private float duration;

    private float timer;
    private float tickTimer;
    private PlayerMovement playerMovement;
    private PlayerEntity playerEntity;

    private float originalSpeed;

    public void Initialize(float slowMult, int dmg, float tickRate, float dur)
    {
        slowMultiplier = slowMult;
        damagePerTick = dmg;
        tickInterval = tickRate;
        duration = dur;

        timer = 0f;
        tickTimer = 0f;

        playerMovement = GetComponent<PlayerMovement>();
        playerEntity = GetComponent<PlayerEntity>();

        if (playerMovement != null)
        {
            if (slowMultiplier <= 0f)
            {
                playerMovement.ApplyRoot(duration);
            }
            else
            {
                originalSpeed = playerMovement.CurrentMoveSpeed;
                float slowedSpeed = originalSpeed * slowMultiplier;
                // Áp dụng tốc độ bị làm chậm
                playerMovement.SetMoveSpeedOverride(slowedSpeed);
            }
        }

        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null) buffMgr.AddBuff("Slime Sludge", "slime_debuff_icon", duration, true);
    }

    public void Refresh(float newDuration)
    {
        // Khi bị trúng lại cọc slime, thời gian debuff sẽ được làm mới
        timer = 0f;
        if (newDuration > duration)
        {
            duration = newDuration;
        }

        if (playerMovement != null && slowMultiplier <= 0f)
        {
            playerMovement.ApplyRoot(duration);
        }

        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null) buffMgr.AddBuff("Slime Sludge", "slime_debuff_icon", duration, true);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        tickTimer += Time.deltaTime;

        // Trừ máu theo thời gian (Damage over Time)
        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            if (playerEntity != null && playerEntity.CurrentHealth > 0)
            {
                var combat = playerEntity.GetComponent<PlayerCombat>();
                if (combat != null && combat.IsDebuffImmune)
                {
                    Destroy(gameObject);
                    return;
                }
                playerEntity.TakeDamage(damagePerTick);
            }
        }

        // Hết thời gian debuff
        if (timer >= duration)
        {
            RemoveDebuff();
        }
    }

    private void RemoveDebuff()
    {
        if (playerMovement != null)
        {
            // Truyền vào 0f để PlayerMovement tự động trả về tốc độ gốc (baseMoveSpeed)
            playerMovement.SetMoveSpeedOverride(0f);
        }
        
        Destroy(this); // Xoá script này khỏi người chơi
    }

    private void OnDestroy()
    {
        // Đảm bảo an toàn: Nếu script bị huỷ đột ngột (ví dụ khi chuyển scene), phải trả lại tốc độ cho người chơi
        if (playerMovement != null)
        {
            playerMovement.SetMoveSpeedOverride(0f);
        }
    }

    /// <summary>
    /// Gọi hàm này để áp dụng hoặc làm mới hiệu ứng Slime lên mục tiêu.
    /// </summary>
    public static void ApplyTo(GameObject target, float slowMult, int dmg, float tickRate, float dur)
    {
        SlimeDebuff existingDebuff = target.GetComponent<SlimeDebuff>();
        if (existingDebuff != null)
        {
            existingDebuff.Refresh(dur);
        }
        else
        {
            SlimeDebuff newDebuff = target.AddComponent<SlimeDebuff>();
            newDebuff.Initialize(slowMult, dmg, tickRate, dur);
        }
    }
}
