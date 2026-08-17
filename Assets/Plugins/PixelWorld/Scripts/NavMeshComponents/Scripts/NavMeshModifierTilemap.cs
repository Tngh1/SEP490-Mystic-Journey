using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NavMeshPlus.Extensions;
using UnityEngine.Tilemaps;

//***********************************************************************************
// Contributed by author jl-randazzo github.com/jl-randazzo
//***********************************************************************************
namespace NavMeshPlus.Components
{
    // Executes mono behaviour operation.
    [AddComponentMenu("Navigation/Navigation Modifier Tilemap", 33)]
    [HelpURL("https://github.com/Unity-Technologies/NavMeshComponents#documentation-draft")]
    [RequireComponent(typeof(Tilemap))]
    [RequireComponent(typeof(NavMeshModifier))]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class NavMeshModifierTilemap : MonoBehaviour
    {
        // Executes tile modifier operation.
        [System.Serializable]
        public struct TileModifier
        {
            public TileBase tile;
            public bool overrideArea;
            [NavMeshArea] public int area;
        }

        private class MatchingTileComparator : IEqualityComparer<TileModifier>
        {
            public static readonly IEqualityComparer<TileModifier> Instance = new MatchingTileComparator();
            // Executes equals operation.
            public bool Equals(TileModifier a, TileModifier b) => a.tile == b.tile;
            // Executes get hash code operation.
            public int GetHashCode(TileModifier tileModifier) => tileModifier.GetHashCode();
        }

        // List of agent types the modifier is applied for.
        // Special values: empty == None, m_AffectedAgents[0] =-1 == All.
        [SerializeField]
        List<int> m_AffectedAgents = new List<int>(new int[] { -1 });    // Default value is All

        [SerializeField]
        List<TileModifier> m_TileModifiers = new List<TileModifier>();

        private Dictionary<TileBase, TileModifier> m_ModifierMap;

        // Executes get modifier map operation.
        public Dictionary<TileBase, TileModifier> GetModifierMap() => m_TileModifiers.Where(mod => mod.tile != null).Distinct(MatchingTileComparator.Instance).ToDictionary(mod => mod.tile);  // Filter records matching the predicate

        void OnEnable()
        {
            CacheModifiers();
        }

        // Executes cache modifiers operation.
        public void CacheModifiers()
        {
            m_ModifierMap = GetModifierMap();
        }

#if UNITY_EDITOR
        // Executes has duplicate tile modifiers operation.
        public bool HasDuplicateTileModifiers()
        {
            return m_TileModifiers.Count != m_TileModifiers.Distinct(MatchingTileComparator.Instance).Count();
        }
#endif // UNITY_EDITOR

        // Executes try get tile modifier operation.
        public virtual bool TryGetTileModifier(Vector3Int coords, Tilemap tilemap, out TileModifier modifier)
        {
            if (tilemap.GetTile(coords) is TileBase tileBase)
            {
                return m_ModifierMap.TryGetValue(tileBase, out modifier);
            }
            modifier = new TileModifier();
            return false;
        }

        // Executes affects agent type operation.
        public bool AffectsAgentType(int agentTypeID)
        {
            if (m_AffectedAgents.Count == 0)
                return false;
            if (m_AffectedAgents[0] == -1)
                return true;
            return m_AffectedAgents.IndexOf(agentTypeID) != -1;
        }
    }
}
