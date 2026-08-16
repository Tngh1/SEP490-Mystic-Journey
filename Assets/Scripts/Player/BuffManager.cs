using UnityEngine;
using System.Collections.Generic;
using MysticJourney.API.Models.Response;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Endpoints;
using System;

[Serializable]
public class ActiveBuff
{
    public string BuffName;
    public string IconName;
    public float DurationRemaining;
    public bool IsDebuff;
}

public class BuffManager : MonoBehaviour
{
    public List<ActiveBuff> ActiveBuffs = new List<ActiveBuff>();

    public event Action OnBuffsUpdated;

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

    public bool IsStatusImmune { get; set; } = false;

    private float _syncTimer = 0f;
    private const float SyncInterval = 30f; // Sync every 30 seconds to be safe

    private void Update()
    {
        bool hasChanges = false;
        
        for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
        {
            var buff = ActiveBuffs[i];
            buff.DurationRemaining -= Time.deltaTime;

            if (buff.DurationRemaining <= 0)
            {
                ActiveBuffs.RemoveAt(i);
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            OnBuffsUpdated?.Invoke();
        }

        // Periodic sync
        _syncTimer += Time.deltaTime;
        if (_syncTimer >= SyncInterval)
        {
            _syncTimer = 0f;
            SyncWithServer();
        }
    }

    public void AddBuff(string name, string iconName, float duration, bool isDebuff)
    {
        name = NormalizeDisplayName(name);
        var combat = GetComponent<PlayerCombat>();
        bool immune = IsStatusImmune || (combat != null && combat.IsDebuffImmune);

        if (isDebuff && immune)
        {
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreateText(transform.position, "Immunity", Color.cyan);
            }
            return; // Immune to debuffs
        }

        var existing = ActiveBuffs.Find(b => b.BuffName == name);
        if (existing != null)
        {
            existing.DurationRemaining = Mathf.Max(existing.DurationRemaining, duration);
        }
        else
        {
            ActiveBuffs.Add(new ActiveBuff
            {
                BuffName = name,
                IconName = iconName,
                DurationRemaining = duration,
                IsDebuff = isDebuff
            });
        }
        OnBuffsUpdated?.Invoke();
    }

    public void ClearAllDebuffs()
    {
        int removed = ActiveBuffs.RemoveAll(b => b.IsDebuff);
        if (removed > 0)
        {
            OnBuffsUpdated?.Invoke();
        }
    }

    public void RemoveBuff(string name)
    {
        int removed = ActiveBuffs.RemoveAll(b => b.BuffName == name);
        if (removed > 0)
        {
            OnBuffsUpdated?.Invoke();
        }
    }

    public bool HasBuff(string name)
    {
        return ActiveBuffs.Exists(b => b.BuffName == name);
    }

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

    private void OnApplicationQuit()
    {
        SyncWithServer();
    }
}
