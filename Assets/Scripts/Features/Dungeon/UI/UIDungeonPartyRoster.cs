using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
public class UIDungeonPartyRoster : MonoBehaviour
{
    [SerializeField] private GameObject partyMemberPrefab;

    private readonly Dictionary<NetworkPlayer, GameObject> _spawnedMembers = new();

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        var rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, -155);
        }

        var vlg = GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (vlg == null)
            vlg = gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childControlHeight = false;
            vlg.childControlWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth  = false;
            vlg.spacing        = 8f;
            vlg.padding        = new UnityEngine.RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.UpperLeft;
        }

        var csf = GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (csf == null)
            csf = gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        if (csf != null)
        {
            csf.verticalFit   = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        }

        ClearRoster();
        RefreshRoster();

        InvokeRepeating(nameof(RefreshRoster), 0.5f, 1f);
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        CancelInvoke(nameof(RefreshRoster));
        ClearRoster();
    }

    // Executes refresh roster operation.
    private void RefreshRoster()
    {
        if (partyMemberPrefab == null) return;

        var players = NetworkPlayer.All;

        List<NetworkPlayer> toRemove = new List<NetworkPlayer>();
        foreach (var member in _spawnedMembers.Keys)
        {
            if (member == null || member.Object == null || !member.Object.IsValid || !players.Contains(member))
            {
                toRemove.Add(member);
            }
        }
        foreach (var removed in toRemove)
        {
            if (_spawnedMembers[removed] != null) Destroy(_spawnedMembers[removed]);
            _spawnedMembers.Remove(removed);
        }

        foreach (var p in players)
        {
            if (p == null || p == NetworkPlayer.Local) continue;

            if (!_spawnedMembers.ContainsKey(p))
            {
                var uiObj = Instantiate(partyMemberPrefab, transform);
                var memberUI = uiObj.GetComponent<UIDungeonPartyMember>();
                if (memberUI != null)
                {
                    memberUI.Init(p);
                }
                _spawnedMembers[p] = uiObj;
            }
        }
    }

    // Executes clear roster operation.
    private void ClearRoster()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        _spawnedMembers.Clear();
    }
}
