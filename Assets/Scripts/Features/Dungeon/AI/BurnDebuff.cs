using UnityEngine;

// Executes mono behaviour operation.
public class BurnDebuff : MonoBehaviour
{
    private float percentPerTick = 3f;
    private int flatDamagePerTick = 0;
    private bool usePercentage = true;

    private float tickInterval = 1f;
    private float duration = 3f;

    private float timer = 0f;
    private float tickTimer = 0f;
    private PlayerEntity playerEntity;
    private NetworkPlayer networkPlayer;

    // Executes initialize percent operation.
    public void InitializePercent(float percentDmg, float interval, float dur)
    {
        var combat = GetComponent<PlayerCombat>();
        var buffMgr = GetComponent<BuffManager>();
        if ((combat != null && combat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune))
        {
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreateText(transform.position, "Immunity", Color.cyan);
            }
            Destroy(this);
            return;
        }

        percentPerTick = percentDmg;
        tickInterval = interval;
        duration = dur;
        usePercentage = true;

        timer = 0f;
        tickTimer = 0f;

        playerEntity = GetComponent<PlayerEntity>();
        networkPlayer = GetComponent<NetworkPlayer>();

        if (buffMgr != null) buffMgr.AddBuff("Burning", "burn_debuff_icon", duration, true);
    }

    // Executes initialize flat operation.
    public void InitializeFlat(int flatDmg, float interval, float dur)
    {
        var combat = GetComponent<PlayerCombat>();
        var buffMgr = GetComponent<BuffManager>();
        if ((combat != null && combat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune))
        {
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreateText(transform.position, "Immunity", Color.cyan);
            }
            Destroy(this);
            return;
        }

        flatDamagePerTick = flatDmg;
        tickInterval = interval;
        duration = dur;
        usePercentage = false;

        timer = 0f;
        tickTimer = 0f;

        playerEntity = GetComponent<PlayerEntity>();
        networkPlayer = GetComponent<NetworkPlayer>();

        if (buffMgr != null) buffMgr.AddBuff("Burning", "burn_debuff_icon", duration, true);
    }

    // Executes refresh operation.
    public void Refresh(float newDuration)
    {
        var combat = GetComponent<PlayerCombat>();
        var buffMgr = GetComponent<BuffManager>();
        if ((combat != null && combat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune))
        {
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreateText(transform.position, "Immunity", Color.cyan);
            }
            Destroy(this);
            return;
        }

        timer = 0f;
        if (newDuration > duration)
        {
            duration = newDuration;
        }

        if (buffMgr != null) buffMgr.AddBuff("Burning", "burn_debuff_icon", duration, true);
    }

    // Per-frame update loop for BurnDebuff.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        var combat = GetComponent<PlayerCombat>();
        var buffMgr = GetComponent<BuffManager>();
        if ((combat != null && combat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune))
        {
            if (buffMgr != null) buffMgr.RemoveBuff("Burning");
            Destroy(this);
            return;
        }

        timer += Time.deltaTime;
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            ApplyBurnDamage();
        }

        if (timer >= duration)
        {
            Destroy(this);
        }
    }

    // Executes apply burn damage operation.
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

    // Executes apply percent to operation.
    public static void ApplyPercentTo(GameObject target, float percentDmg = 3f, float interval = 1f, float dur = 3f)
    {
        var combat = target.GetComponent<PlayerCombat>();
        var buffMgr = target.GetComponent<BuffManager>();
        if ((combat != null && combat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune))
        {
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreateText(target.transform.position, "Immunity", Color.cyan);
            }
            return;
        }

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

    // Executes apply flat to operation.
    public static void ApplyFlatTo(GameObject target, int flatDmg = 5, float interval = 1f, float dur = 3f)
    {
        var combat = target.GetComponent<PlayerCombat>();
        var buffMgr = target.GetComponent<BuffManager>();
        if ((combat != null && combat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune))
        {
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreateText(target.transform.position, "Immunity", Color.cyan);
            }
            return;
        }

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
