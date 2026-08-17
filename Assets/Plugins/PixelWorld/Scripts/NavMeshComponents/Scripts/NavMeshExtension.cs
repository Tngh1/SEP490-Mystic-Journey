using NavMeshPlus.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NavMeshPlus.Extensions
{
    // Executes mono behaviour operation.
    public abstract class NavMeshExtension: MonoBehaviour
    {
        // Executes order operation.
        public int Order { get; protected set; }
        // Executes collect sources operation.
        public virtual void CollectSources(NavMeshSurface surface, List<NavMeshBuildSource> sources, NavMeshBuilderState navNeshState) { }
        // Executes calculate world bounds operation.
        public virtual void CalculateWorldBounds(NavMeshSurface surface, List<NavMeshBuildSource> sources, NavMeshBuilderState navNeshState) { }
        // Executes post collect sources operation.
        public virtual void PostCollectSources(NavMeshSurface surface, List<NavMeshBuildSource> sources, NavMeshBuilderState navNeshState) { }
        // Executes nav mesh surface owner operation.
        public NavMeshSurface NavMeshSurfaceOwner
        {
            get
            {
                if (m_navMeshOwner == null)  // Entity not found — short-circuit with appropriate error result
                    m_navMeshOwner = GetComponent<NavMeshSurface>();
                return m_navMeshOwner;
            }
        }
        NavMeshSurface m_navMeshOwner;

        // Initializes internal component caches and dependencies for NavMeshExtension upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        protected virtual void Awake()
        {
            ConnectToVcam(true);
        }
#if UNITY_EDITOR
        // Executes on script reload operation.
        [UnityEditor.Callbacks.DidReloadScripts]
        static void OnScriptReload()
        {
            var extensions = Resources.FindObjectsOfTypeAll(
                typeof(NavMeshExtension)) as NavMeshExtension[];
            foreach (var e in extensions)
                e.ConnectToVcam(true);
        }
#endif
        // Callback invoked when NavMeshExtension becomes enabled and active in the scene hierarchy.
        // Subscribes to global game events and refreshes visible UI displays.
        protected virtual void OnEnable() { }
        // Cleanup callback executed when NavMeshExtension is destroyed.
        // Unsubscribes from events, cancels active coroutines, and prevents memory leaks.
        protected virtual void OnDestroy()
        {
            ConnectToVcam(false);
        }
        // Executes connect to vcam operation.
        protected virtual void ConnectToVcam(bool connect)
        {
            if (connect && NavMeshSurfaceOwner == null)
                Debug.LogError("NevMeshExtension requires a NavMeshSurface component");
            if (NavMeshSurfaceOwner != null)  // Entity exists — proceed with conditional branch
            {
                if (connect)
                    NavMeshSurfaceOwner.NevMeshExtensions.Add(this, Order);
                else
                    NavMeshSurfaceOwner.NevMeshExtensions.Remove(this);  // Mark entity for deletion in the next SaveChanges call
            }
        }
    }
}
