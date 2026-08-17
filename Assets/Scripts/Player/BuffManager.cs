using UnityEngine;
using System.Collections.Generic;
using MysticJourney.API.Models.Response;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Endpoints;
using System;

// Initializes a new default instance of the ActiveBuff class.
[Serializable]
public class ActiveBuff
{
    public string BuffName;
    public string IconName;
    public float DurationRemaining;
    public bool IsDebuff;
}

// Executes mono behaviour operation.
// Validates input parameters against null or empty values.
public class BuffManager : MonoBehaviour
{
    public List<ActiveBuff> ActiveBuffs = new List<ActiveBuff>();

    public event Action OnBuffsUpdated;

    // Executes normalize display name operation.
    // Validates input parameters against null or empty values.
    private static string NormalizeDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Status Effect";
        switch (name.Trim())
        {
            case "Bảo Hộ": return "Protection";
            case "Kháng Hiệu Ứng": return "Status Immunity";
            case "Lời Nguyền Bóng Đêm": return "Darkness Curse";
            default: return name;
        }
    }

    // Executes is status immune operation.
    public bool IsStatusImmune { get; set; } = false;

    private float _syncTimer = 0f;
    private const float SyncInterval = 30f;

    // Decrements active buff/debuff durations each frame, purges expired effects, and triggers periodic server sync.
    private void Update()
    {
        bool hasChanges = false;

        for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
        {
            var buff = ActiveBuffs[i];
            buff.DurationRemaining -= Time.deltaTime; // Decrement remaining duration by delta time

            if (buff.DurationRemaining <= 0)
            {
                ActiveBuffs.RemoveAt(i); // Remove expired buff/debuff from list
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            OnBuffsUpdated?.Invoke(); // Notify HUD to redraw active buff icons
        }

        _syncTimer += Time.deltaTime;
        if (_syncTimer >= SyncInterval)
        {
            _syncTimer = 0f;
            SyncWithServer(); // Periodic 30s background sync to backend
        }
    }

    // Applies a buff or debuff to the player, respecting debuff immunity and stacking rules.
    public void AddBuff(string name, string iconName, float duration, bool isDebuff)
    {
        name = NormalizeDisplayName(name); // Normalize English status effect title
        var combat = GetComponent<PlayerCombat>();
        bool immune = IsStatusImmune || (combat != null && combat.IsDebuffImmune);

        if (isDebuff && immune)
        {
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreateText(transform.position, "Immunity", Color.cyan); // Show Immunity float text
            }
            return; // Block debuff if immune
        }

        var existing = ActiveBuffs.Find(b => b.BuffName == name);
        if (existing != null)
        {
            existing.DurationRemaining = Mathf.Max(existing.DurationRemaining, duration); // Refresh duration of existing buff
        }
        else
        {
            ActiveBuffs.Add(new ActiveBuff
            {
                BuffName = name,
                IconName = iconName,
                DurationRemaining = duration,
                IsDebuff = isDebuff
            }); // Insert new active buff entry
        }
        OnBuffsUpdated?.Invoke(); // Refresh UI tray
    }

    // Purges all negative status effects/debuffs (e.g. cleanses, immunity skills).
    public void ClearAllDebuffs()
    {
        int removed = ActiveBuffs.RemoveAll(b => b.IsDebuff); // Remove all debuff entries
        if (removed > 0)
        {
            OnBuffsUpdated?.Invoke(); // Refresh UI
        }
    }

    // Removes a specific buff by name.
    public void RemoveBuff(string name)
    {
        int removed = ActiveBuffs.RemoveAll(b => b.BuffName == name); // Remove matching buff
        if (removed > 0)
        {
            OnBuffsUpdated?.Invoke(); // Refresh UI
        }
    }

    // Checks whether the player currently has an active buff with the specified name.
    public bool HasBuff(string name)
    {
        return ActiveBuffs.Exists(b => b.BuffName == name); // Query existence in list
    }

    // Executes load from server operation.
    public void LoadFromServer(List<PlayerBuffDTO> serverBuffs)
    {
        ActiveBuffs.Clear();
        if (serverBuffs != null)
        {
            foreach (var b in serverBuffs)
            {
                ActiveBuffs.Add(new ActiveBuff
                {
                    BuffName = NormalizeDisplayName(b.BuffName),
                    IconName = b.IconName,
                    DurationRemaining = b.DurationRemaining,
                    IsDebuff = b.IsDebuff
                });
            }
        }
        OnBuffsUpdated?.Invoke();
    }

    // Executes sync with server operation.
    public void SyncWithServer()
    {
        var request = new UpdatePlayerBuffsRequest();
        foreach (var b in ActiveBuffs)
        {
            request.Buffs.Add(new PlayerBuffDTO
            {
                BuffName = b.BuffName,
                IconName = b.IconName,
                DurationRemaining = b.DurationRemaining,
                IsDebuff = b.IsDebuff
            });
        }
        if (CharacterApi.Instance != null)
        {
            CharacterApi.Instance.SyncBuffs(request, null, null);
        }
    }

    // Executes on application quit operation.
    private void OnApplicationQuit()
    {
        SyncWithServer();
    }
}
