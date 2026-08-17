using NavMeshPlus.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace NavMeshPlus.Extensions
{
    // Executes nav mesh extension operation.
    [ExecuteAlways]
    [AddComponentMenu("Navigation/Navigation RootSources2d", 30)]
    public class RootSources2d: NavMeshExtension
    {
        [SerializeField]
        private List<GameObject> _rootSources;

        // Executes root sources operation.
        public List<GameObject> RootSources { get => _rootSources; set => _rootSources = value; }

        // Initializes internal component caches and dependencies for RootSources2d upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        protected override void Awake()
        {
            Order = -1000;
            base.Awake();
        }

        // Executes collect sources operation.
        public override void CollectSources(NavMeshSurface surface, List<NavMeshBuildSource> sources, NavMeshBuilderState navNeshState)
        {
            navNeshState.roots = _rootSources;
        }
    }
}
