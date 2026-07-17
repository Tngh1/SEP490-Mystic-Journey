using UnityEngine;
using System.Collections.Generic;

public class UIBuffPanel : MonoBehaviour
{
    [SerializeField] private UIBuffIcon iconPrefab;
    [SerializeField] private Transform container;

    private BuffManager _buffManager;
    private Dictionary<string, UIBuffIcon> _activeIcons = new Dictionary<string, UIBuffIcon>();

    private void Update()
    {
        if (_buffManager == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _buffManager = player.GetComponent<BuffManager>();
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

        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
        _activeIcons.Clear();

        foreach (var buff in _buffManager.ActiveBuffs)
        {
            var icon = Instantiate(iconPrefab, container);
            icon.Setup(buff);
            _activeIcons.Add(buff.BuffName, icon);
        }
    }
    
    private void OnDestroy()
    {
        if (_buffManager != null) _buffManager.OnBuffsUpdated -= Refresh;
    }
}
