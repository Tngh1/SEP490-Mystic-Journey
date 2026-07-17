using System.Collections.Generic;
using UnityEngine;

namespace UI.Combat
{
    public class UIBuffManager : MonoBehaviour
    {
        [SerializeField] private GameObject buffIconPrefab;
        [SerializeField] private Transform buffContainer;

        private Dictionary<string, BuffIconUI> _activeBuffIcons = new Dictionary<string, BuffIconUI>();

        public void AddOrUpdateBuffIcon(string buffName, Sprite icon, float duration, bool isDebuff)
        {
            if (_activeBuffIcons.TryGetValue(buffName, out var existingIcon))
            {
                if (existingIcon != null)
                {
                    existingIcon.Setup(buffName, icon, duration, isDebuff);
                    return;
                }
                else
                {
                    _activeBuffIcons.Remove(buffName);
                }
            }

            if (buffIconPrefab != null && buffContainer != null)
            {
                var go = Instantiate(buffIconPrefab, buffContainer);
                var buffUI = go.GetComponent<BuffIconUI>();
                if (buffUI != null)
                {
                    buffUI.Setup(buffName, icon, duration, isDebuff);
                    _activeBuffIcons[buffName] = buffUI;
                }
            }
        }

        public void RemoveBuffIcon(string buffName)
        {
            if (_activeBuffIcons.TryGetValue(buffName, out var icon))
            {
                if (icon != null)
                {
                    Destroy(icon.gameObject);
                }
                _activeBuffIcons.Remove(buffName);
            }
        }
        
        public void ClearAll()
        {
            foreach (var kvp in _activeBuffIcons)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _activeBuffIcons.Clear();
        }

        private void Start()
        {
            // Assuming the UIBuffManager is on the same GameObject as BuffManager, or we find it
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var bm = player.GetComponent<BuffManager>();
                if (bm != null)
                {
                    bm.OnBuffsUpdated += () => RefreshUI(bm);
                    RefreshUI(bm);
                }
            }
        }

        private void RefreshUI(BuffManager bm)
        {
            // Simple refresh logic: add or update all, then remove expired ones
            HashSet<string> currentBuffs = new HashSet<string>();

            foreach (var buff in bm.ActiveBuffs)
            {
                currentBuffs.Add(buff.BuffName);
                
                // Try to load icon from Resources (assume iconName matches filename in Resources/Icons/Buffs)
                Sprite iconSprite = null;
                if (!string.IsNullOrEmpty(buff.IconName))
                {
                    iconSprite = Resources.Load<Sprite>($"Icons/Buffs/{buff.IconName}");
                }
                
                AddOrUpdateBuffIcon(buff.BuffName, iconSprite, buff.DurationRemaining, buff.IsDebuff);
            }

            // Remove buffs that are no longer active
            List<string> toRemove = new List<string>();
            foreach (var key in _activeBuffIcons.Keys)
            {
                if (!currentBuffs.Contains(key))
                {
                    toRemove.Add(key);
                }
            }

            foreach (var key in toRemove)
            {
                RemoveBuffIcon(key);
            }
        }
    }
}
