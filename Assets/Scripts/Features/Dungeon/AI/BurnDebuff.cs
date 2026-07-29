using UnityEngine;

/// <summary>
/// Hiệu ứng Thiêu đốt (Burn Debuff) áp dụng lên Người chơi.
/// Tồn tại trong `duration` (mặc định 3s).
/// Mỗi giây trừ % Máu tối đa (`percentPerTick`, mặc định 3%) hoặc sát thương cố định (`flatDamagePerTick`).
/// </summary>
public class BurnDebuff : MonoBehaviour
{
    private float percentPerTick = 3f; // 3% máu tối đa mỗi giây
    private int flatDamagePerTick = 0;
    private bool usePercentage = true;

    private float tickInterval = 1f;
    private float duration = 3f;

    private float timer = 0f;
    private float tickTimer = 0f;
    private PlayerEntity playerEntity;
    private NetworkPlayer networkPlayer;

    public void InitializePercent(float percentDmg, float interval, float dur)
    {
        percentPerTick = percentDmg;
        tickInterval = interval;
        duration = dur;
        usePercentage = true;

        timer = 0f;
        tickTimer = 0f;

        playerEntity = GetComponent<PlayerEntity>();
        networkPlayer = GetComponent<NetworkPlayer>();

        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null) buffMgr.AddBuff("Burning", "burn_debuff_icon", duration, false);
    }

    public void InitializeFlat(int flatDmg, float interval, float dur)
    {
        flatDamagePerTick = flatDmg;
        tickInterval = interval;
        duration = dur;
        usePercentage = false;

        timer = 0f;
        tickTimer = 0f;

        playerEntity = GetComponent<PlayerEntity>();
        networkPlayer = GetComponent<NetworkPlayer>();

        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null) buffMgr.AddBuff("Burning", "burn_debuff_icon", duration, false);
    }

    public void Refresh(float newDuration)
    {
        timer = 0f;
        if (newDuration > duration)
        {
            duration = newDuration;
        }

        var buffMgr = GetComponent<BuffManager>();
        if (buffMgr != null) buffMgr.AddBuff("Burning", "burn_debuff_icon", duration, false);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        tickTimer += Time.deltaTime;

        // Gây sát thương thiêu đốt theo mỗi khoảng thời gian tickInterval (1s)
        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            ApplyBurnDamage();
        }

        // Hết thời gian thiêu đốt -> xóa debuff
        if (timer >= duration)
        {
            Destroy(this);
        }
    }

    private void ApplyBurnDamage()
    {
        int damageToDeal = 0;

        if (usePercentage)
        {
            int maxHp = 500;
            if (networkPlayer != null && networkPlayer.MaxHp > 0)
            {
                maxHp = networkPlayer.MaxHp;
            }
            else if (playerEntity != null && playerEntity.MaxHealth > 0)
            {
                maxHp = playerEntity.MaxHealth;
            }
            else if (PlayerEntity.Instance != null && PlayerEntity.Instance.MaxHealth > 0)
            {
                maxHp = PlayerEntity.Instance.MaxHealth;
            }

            // Tính 3% máu tối đa
            damageToDeal = Mathf.Max(1, Mathf.RoundToInt(maxHp * (percentPerTick / 100f)));
        }
        else
        {
            damageToDeal = flatDamagePerTick;
        }

        if (networkPlayer != null && networkPlayer.Object != null && networkPlayer.Object.IsValid)
        {
            networkPlayer.RequestDamage(damageToDeal);
        }
        else if (playerEntity != null && playerEntity.CurrentHealth > 0)
        {
            playerEntity.TakeDamage(damageToDeal);
        }
        else if (PlayerEntity.Instance != null && PlayerEntity.Instance.CurrentHealth > 0)
        {
            PlayerEntity.Instance.TakeDamage(damageToDeal);
        }
    }

    /// <summary>
    /// Hàm static áp dụng hiệu ứng Thiêu đốt theo % Máu tối đa (mặc định 3% mỗi 1s trong 3s).
    /// </summary>
    public static void ApplyPercentTo(GameObject target, float percentDmg = 3f, float interval = 1f, float dur = 3f)
    {
        BurnDebuff existing = target.GetComponent<BurnDebuff>();
        if (existing != null)
        {
            existing.Refresh(dur);
        }
        else
        {
            BurnDebuff newBurn = target.AddComponent<BurnDebuff>();
            newBurn.InitializePercent(percentDmg, interval, dur);
        }
    }

    /// <summary>
    /// Hàm static áp dụng hiệu ứng Thiêu đốt theo sát thương cố định.
    /// </summary>
    public static void ApplyFlatTo(GameObject target, int flatDmg = 5, float interval = 1f, float dur = 3f)
    {
        BurnDebuff existing = target.GetComponent<BurnDebuff>();
        if (existing != null)
        {
            existing.Refresh(dur);
        }
        else
        {
            BurnDebuff newBurn = target.AddComponent<BurnDebuff>();
            newBurn.InitializeFlat(flatDmg, interval, dur);
        }
    }
}
