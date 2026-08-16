using UnityEngine;
using System.Collections.Generic;

public class UIBuffPanel : MonoBehaviour
{
    [SerializeField] private UIBuffIcon iconPrefab;
    [SerializeField] private Transform container;

    private BuffManager _buffManager;
    private Dictionary<string, UIBuffIcon> _activeIcons = new Dictionary<string, UIBuffIcon>();
    private bool _autoBind = true;

    public void Init(BuffManager manager)
    {
        _autoBind = false;
        if (_buffManager != null) _buffManager.OnBuffsUpdated -= Refresh;
        _buffManager = manager;
        if (_buffManager != null)
        {
            _buffManager.OnBuffsUpdated += Refresh;
            Refresh();
        }
    }

    private void Update()
    {
        if (_autoBind && _buffManager == null)
        {
            if (PlayerEntity.Instance != null)
            {
                _buffManager = PlayerEntity.Instance.GetComponent<BuffManager>();
                if (_buffManager != null)
                {
                    _buffManager.OnBuffsUpdated += Refresh;
                    Refresh();
                }
            }
        }
    }

    private void Refresh()
    {
        if (_buffManager == null) return;
        
        // Auto-assign container to self if user forgot to drag it in
        if (container == null) container = transform;

        // Tự động tắt tính năng Force Expand Width của HorizontalLayoutGroup để tránh icon bị giãn cách xa nhau
        var layoutGroup = container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.childForceExpandWidth = false;
        }

        HashSet<string> currentBuffs = new HashSet<string>();

        foreach (var buff in _buffManager.ActiveBuffs)
        {
            currentBuffs.Add(buff.BuffName);

            if (_activeIcons.TryGetValue(buff.BuffName, out var icon))
            {
                if (icon != null)
                {
                    icon.Setup(buff);
                    // Đảm bảo icon nằm cuối danh sách (nếu muốn thứ tự không bị đổi, có thể tính toán lại sibling index)
                }
                else
                {
                    _activeIcons.Remove(buff.BuffName);
                }
            }
            
            if (!_activeIcons.ContainsKey(buff.BuffName))
            {
                var newIcon = Instantiate(iconPrefab, container);
                newIcon.transform.localScale = Vector3.one;
                newIcon.transform.localPosition = new Vector3(newIcon.transform.localPosition.x, newIcon.transform.localPosition.y, 0);
                newIcon.Setup(buff);
                _activeIcons.Add(buff.BuffName, newIcon);
            }
        }

        // Remove expired
        List<string> toRemove = new List<string>();
        foreach (var kvp in _activeIcons)
        {
            if (!currentBuffs.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            _activeIcons.Remove(key);
        }
    }
    
    private void OnDestroy()
    {
        if (_buffManager != null) _buffManager.OnBuffsUpdated -= Refresh;
    }
}
