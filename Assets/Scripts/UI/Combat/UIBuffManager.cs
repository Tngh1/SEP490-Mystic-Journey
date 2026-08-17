using System.Collections.Generic;
using UnityEngine;

namespace UI.Combat
{
    // Executes core business logic for mono behaviour.
    public class UIBuffManager : MonoBehaviour
    {
        [SerializeField] private GameObject buffIconPrefab;
        [SerializeField] private Transform buffContainer;

        private Dictionary<string, BuffIconUI> _activeBuffIcons = new Dictionary<string, BuffIconUI>();

        // Executes core business logic for add or update buff icon.
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

        // Executes core business logic for remove buff icon.
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

        // Executes core business logic for clear all.
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

        // Performs startup initialization for UIBuffManager on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
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

        // Executes core business logic for refresh ui.
        // Logic details: validates required non-empty string arguments.
        private void RefreshUI(BuffManager bm)
        {
            HashSet<string> currentBuffs = new HashSet<string>();

            foreach (var buff in bm.ActiveBuffs)
            {
                currentBuffs.Add(buff.BuffName);

                Sprite iconSprite = null;
                if (!string.IsNullOrEmpty(buff.IconName))
                {
                    Sprite[] sprites = Resources.LoadAll<Sprite>($"Icons/Effects/{buff.IconName}");
                    if (sprites != null && sprites.Length > 0)
                    {
                        iconSprite = sprites[0];
                    }
                }

                AddOrUpdateBuffIcon(buff.BuffName, iconSprite, buff.DurationRemaining, buff.IsDebuff);
            }

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
