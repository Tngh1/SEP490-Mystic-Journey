using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the Party Roster UI inside a dungeon.
/// Automatically detects party members and instantiates their HP/MP tracking prefabs.
/// </summary>
public class UIDungeonPartyRoster : MonoBehaviour
{
    [SerializeField] private GameObject partyMemberPrefab;
    
    private readonly Dictionary<NetworkPlayer, GameObject> _spawnedMembers = new();

    private void OnEnable()
    {
        // Anchor to top-left corner, pivot top-left so list grows downward from that point.
        // Position it just below the local player's avatar/HP bar (TopBar Button height ~150px).
        var rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);            // top-left pivot = left-aligned
            rect.anchoredPosition = new Vector2(0, -155); // align with avatar
        }

        // VerticalLayoutGroup stacks member frames top-to-bottom automatically.
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

        // ContentSizeFitter lets the container auto-resize to its children.
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
        
        // Listen to spawn/despawn events if NetworkPlayer provides them,
        // otherwise we just poll periodically or assume roster doesn't change mid-dungeon.
        InvokeRepeating(nameof(RefreshRoster), 2f, 5f); 
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(RefreshRoster));
        ClearRoster();
    }

    private void RefreshRoster()
    {
        if (partyMemberPrefab == null) return;

        var players = NetworkPlayer.All;
        
        // Remove disconnected
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

        // Add new
        foreach (var p in players)
        {
            if (p == null || p == NetworkPlayer.Local) continue; // Skip self (TopBar handles local HP)

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

    private void ClearRoster()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        _spawnedMembers.Clear();
    }
}
