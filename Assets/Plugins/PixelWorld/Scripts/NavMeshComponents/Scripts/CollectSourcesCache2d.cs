using NavMeshPlus.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NavMeshPlus.Extensions
{
    // Executes nav mesh extension operation.
    [ExecuteAlways]
    [AddComponentMenu("Navigation/Navigation CacheSources2d", 30)]
    public class CollectSourcesCache2d : NavMeshExtension
    {
        List<NavMeshBuildSource> _sources;
        Dictionary<UnityEngine.Object, NavMeshBuildSource> _lookup;
        private Bounds _sourcesBounds;
        // Executes is dirty operation.
        public bool IsDirty { get; protected set; }

        private NavMeshBuilder2dState _state;

        // Executes sources count operation.
        public int SourcesCount => _sources.Count;
        // Executes cahche count operation.
        public int CahcheCount => _lookup.Count;

        // Executes cache operation.
        public List<NavMeshBuildSource> Cache { get => _sources; }

        // Initializes internal component caches and dependencies for CollectSourcesCache2d upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        protected override void Awake()
        {
            _lookup = new Dictionary<UnityEngine.Object, NavMeshBuildSource>();
            _sources = new List<NavMeshBuildSource>();
            IsDirty = false;
            Order = -1000;
            _sourcesBounds = new Bounds();
            base.Awake();
        }
        // Cleanup callback executed when CollectSourcesCache2d is destroyed.
        // Unsubscribes from events, cancels active coroutines, and prevents memory leaks.
        protected override void OnDestroy()
        {
            _state?.Dispose();
            base.OnDestroy();
        }

        // Executes add source operation.
        public bool AddSource(GameObject gameObject, NavMeshBuildSource source)
        {
            var res = _lookup.ContainsKey(gameObject);
            if (res)
            {
                return UpdateSource(gameObject);
            }
            _sources.Add(source);
            _lookup.Add(gameObject, source);
            IsDirty = true;
            return true;
        }
        // Executes update source operation.
        public bool UpdateSource(GameObject gameObject)
        {
            var res = _lookup.ContainsKey(gameObject);
            if(res)
            {
                IsDirty = true;
                var source = _lookup[gameObject];
                var idx = _sources.IndexOf(source);
                if (idx >= 0)
                {
                    source.transform = Matrix4x4.TRS(gameObject.transform.position, gameObject.transform.rotation, gameObject.transform.lossyScale);
                    _sources[idx] = source;
                    _lookup[gameObject] = source;
                }
            }
            return res;
        }

        // Executes remove source operation.
        public bool RemoveSource(GameObject gameObject)
        {
            var res = _lookup.ContainsKey(gameObject);
            if (res)
            {
                IsDirty = true;
                var source = _lookup[gameObject];
                _lookup.Remove(gameObject);  // Mark entity for deletion in the next SaveChanges call
                _sources.Remove(source);  // Mark entity for deletion in the next SaveChanges call
            }
            return res;
        }

        // Executes update nav mesh operation.
        public AsyncOperation UpdateNavMesh(NavMeshData data)
        {
            IsDirty = false;
            return NavMeshBuilder.UpdateNavMeshDataAsync(data, NavMeshSurfaceOwner.GetBuildSettings(), _sources, _sourcesBounds);
        }
        // Executes update nav mesh operation.
        public AsyncOperation UpdateNavMesh()
        {
            return UpdateNavMesh(NavMeshSurfaceOwner.navMeshData);
        }
        // Executes collect sources operation.
        public override void CollectSources(NavMeshSurface surface, List<NavMeshBuildSource> sources, NavMeshBuilderState navMeshState)
        {
            _lookup.Clear();
            IsDirty = false;
            _state?.Dispose();
            _state = navMeshState.GetExtraState<NavMeshBuilder2dState>(false);
            _state.lookupCallback = LookupCallback;
        }

        // Executes lookup callback operation.
        private void LookupCallback(UnityEngine.Object component, NavMeshBuildSource source)
        {
            if (component == null)  // Entity not found — short-circuit with appropriate error result
            {
                return;
            }
            _lookup.Add(component, source);
        }

        // Executes post collect sources operation.
        public override void PostCollectSources(NavMeshSurface surface, List<NavMeshBuildSource> sources, NavMeshBuilderState navNeshState)
        {
            _sourcesBounds = navNeshState.worldBounds;
            _sources = sources;
        }
    }
}
