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
        
        // Auto-assign container to self if user forgot to drag it in
        if (container == null) container = transform;

        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
        _activeIcons.Clear();

        foreach (var buff in _buffManager.ActiveBuffs)
        {
            var icon = Instantiate(iconPrefab, container);
            icon.transform.localScale = Vector3.one;
            icon.transform.localPosition = new Vector3(icon.transform.localPosition.x, icon.transform.localPosition.y, 0);
            icon.Setup(buff);
            _activeIcons.Add(buff.BuffName, icon);
        }
    }
    
    private void OnDestroy()
    {
        if (_buffManager != null) _buffManager.OnBuffsUpdated -= Refresh;
    }
}
