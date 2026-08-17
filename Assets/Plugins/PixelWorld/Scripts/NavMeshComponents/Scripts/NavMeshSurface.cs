using NavMeshPlus.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace NavMeshPlus.Components
{
    // Executes collect objects operation.
    // Evaluates conditions and returns a boolean result.
    public enum CollectObjects
    {
        All = 0,
        Volume = 1,
        Children = 2,
    }

    // Executes mono behaviour operation.
    [ExecuteAlways]
    [DefaultExecutionOrder(-102)]
    [AddComponentMenu("Navigation/Navigation Surface", 30)]
    [HelpURL("https://github.com/Unity-Technologies/NavMeshComponents#documentation-draft")]
    public class NavMeshSurface : MonoBehaviour
    {
        [SerializeField, NavMeshAgent]
        int m_AgentTypeID;
        // Executes agent type id operation.
        public int agentTypeID { get { return m_AgentTypeID; } set { m_AgentTypeID = value; } }

        [SerializeField]
        CollectObjects m_CollectObjects = CollectObjects.All;
        // Executes collect objects operation.
        public CollectObjects collectObjects { get { return m_CollectObjects; } set { m_CollectObjects = value; } }

        [SerializeField]
        Vector3 m_Size = new Vector3(10.0f, 10.0f, 10.0f);
        // Executes size operation.
        public Vector3 size { get { return m_Size; } set { m_Size = value; } }

        [SerializeField]
        Vector3 m_Center = new Vector3(0, 2.0f, 0);
        // Executes center operation.
        public Vector3 center { get { return m_Center; } set { m_Center = value; } }

        [SerializeField]
        LayerMask m_LayerMask = ~0;
        // Executes layer mask operation.
        public LayerMask layerMask { get { return m_LayerMask; } set { m_LayerMask = value; } }

        [SerializeField]
        NavMeshCollectGeometry m_UseGeometry = NavMeshCollectGeometry.RenderMeshes;
        // Executes use geometry operation.
        public NavMeshCollectGeometry useGeometry { get { return m_UseGeometry; } set { m_UseGeometry = value; } }

        [SerializeField, NavMeshArea]
        int m_DefaultArea;
        // Executes default area operation.
        public int defaultArea { get { return m_DefaultArea; } set { m_DefaultArea = value; } }

        [SerializeField]
        bool m_IgnoreNavMeshAgent = true;
        // Executes ignore nav mesh agent operation.
        public bool ignoreNavMeshAgent { get { return m_IgnoreNavMeshAgent; } set { m_IgnoreNavMeshAgent = value; } }

        [SerializeField]
        bool m_IgnoreNavMeshObstacle = true;
        // Executes ignore nav mesh obstacle operation.
        public bool ignoreNavMeshObstacle { get { return m_IgnoreNavMeshObstacle; } set { m_IgnoreNavMeshObstacle = value; } }

        [SerializeField]
        bool m_OverrideTileSize;
        // Executes override tile size operation.
        public bool overrideTileSize { get { return m_OverrideTileSize; } set { m_OverrideTileSize = value; } }
        [SerializeField]
        int m_TileSize = 256;
        // Executes tile size operation.
        public int tileSize { get { return m_TileSize; } set { m_TileSize = value; } }
        [SerializeField]
        bool m_OverrideVoxelSize;
        // Executes override voxel size operation.
        public bool overrideVoxelSize { get { return m_OverrideVoxelSize; } set { m_OverrideVoxelSize = value; } }
        [SerializeField]
        float m_VoxelSize;
        // Executes voxel size operation.
        public float voxelSize { get { return m_VoxelSize; } set { m_VoxelSize = value; } }

        // Currently not supported advanced options
        [SerializeField]
        bool m_BuildHeightMesh;
        // Executes build height mesh operation.
        public bool buildHeightMesh { get { return m_BuildHeightMesh; } set { m_BuildHeightMesh = value; } }

        [SerializeField]
        float m_MinRegionArea = 0;
        // Executes min region area operation.
        public float minRegionArea { get {  return m_MinRegionArea; } set { m_MinRegionArea = value; } }
        
        [SerializeField]
        bool m_HideEditorLogs;
        // Executes hide editor logs operation.
        public bool hideEditorLogs { get { return m_HideEditorLogs; } set { m_HideEditorLogs = value; } }

        // Reference to whole scene navmesh data asset.
        [UnityEngine.Serialization.FormerlySerializedAs("m_BakedNavMeshData")]
        [SerializeField]
        NavMeshData m_NavMeshData;
        // Executes nav mesh data operation.
        public NavMeshData navMeshData { get { return m_NavMeshData; } set { m_NavMeshData = value; } }

        // Do not serialize - runtime only state.
        NavMeshDataInstance m_NavMeshDataInstance;
        Vector3 m_LastPosition = Vector3.zero;
        Quaternion m_LastRotation = Quaternion.identity;

        // Executes nav mesh data instance operation.
        public NavMeshDataInstance navMeshDataInstance => m_NavMeshDataInstance;

        static readonly List<NavMeshSurface> s_NavMeshSurfaces = new List<NavMeshSurface>();
        // Executes nev mesh extensions operation.
        public INavMeshExtensionsProvider NevMeshExtensions { get; set; } = new NavMeshExtensionsProvider();

        // Executes active surfaces operation.
        public static List<NavMeshSurface> activeSurfaces
        {
            get { return s_NavMeshSurfaces; }
        }

        void OnEnable()
        {
            Register(this);
            AddData();
        }

        void OnDisable()
        {
            RemoveData();
            Unregister(this);
        }

        // Executes add data operation.
        public void AddData()
        {
#if UNITY_EDITOR
            var isInPreviewScene = EditorSceneManager.IsPreviewSceneObject(this);
            var isPrefab = isInPreviewScene || EditorUtility.IsPersistent(this);
            if (isPrefab)
            {
                //Debug.LogFormat("NavMeshData from {0}.{1} will not be added to the NavMesh world because the gameObject is a prefab.",
                //    gameObject.name, name);
                return;
            }
#endif
            if (m_NavMeshDataInstance.valid)
                return;

            if (m_NavMeshData != null)  // Entity exists — proceed with conditional branch
            {
                m_NavMeshDataInstance = NavMesh.AddNavMeshData(m_NavMeshData, transform.position, transform.rotation);
                m_NavMeshDataInstance.owner = this;
            }

            m_LastPosition = transform.position;
            m_LastRotation = transform.rotation;
        }

        // Executes remove data operation.
        public void RemoveData()
        {
            m_NavMeshDataInstance.Remove();  // Mark entity for deletion in the next SaveChanges call
            m_NavMeshDataInstance = new NavMeshDataInstance();
        }

        // Executes get build settings operation.
        public NavMeshBuildSettings GetBuildSettings()
        {
            var buildSettings = NavMesh.GetSettingsByID(m_AgentTypeID);
            if (buildSettings.agentTypeID == -1)
            {
                if (!m_HideEditorLogs) Debug.LogWarning("No build settings for agent type ID " + agentTypeID, this);
                buildSettings.agentTypeID = m_AgentTypeID;
            }

            if (overrideTileSize)
            {
                buildSettings.overrideTileSize = true;
                buildSettings.tileSize = tileSize;
            }
            if (overrideVoxelSize)
            {
                buildSettings.overrideVoxelSize = true;
                buildSettings.voxelSize = voxelSize;
            }

            buildSettings.minRegionArea = minRegionArea;
            return buildSettings;
        }

        // Executes build nav mesh operation.
        public void BuildNavMesh()
        {
            using var builderState = new NavMeshBuilderState() { };

            var sources = CollectSources(builderState);

            // Use unscaled bounds - this differs in behaviour from e.g. collider components.
            // But is similar to reflection probe - and since navmesh data has no scaling support - it is the right choice here.
            var sourcesBounds = new Bounds(m_Center, Abs(m_Size));
            if (m_CollectObjects == CollectObjects.All || m_CollectObjects == CollectObjects.Children)
            {
                sourcesBounds = CalculateWorldBounds(sources);
            }
            builderState.worldBounds = sourcesBounds;
            for (int i = 0; i < NevMeshExtensions.Count; ++i)
            {
                NevMeshExtensions[i].PostCollectSources(this, sources, builderState);
            }
            var data = NavMeshBuilder.BuildNavMeshData(GetBuildSettings(),
                    sources, sourcesBounds, transform.position, transform.rotation);

            if (data != null)  // Entity exists — proceed with conditional branch
            {
                data.name = gameObject.name;
                RemoveData();
                m_NavMeshData = data;
                if (isActiveAndEnabled)
                    AddData();
            }
        }

        // Source: https://github.com/Unity-Technologies/NavMeshComponents/issues/97#issuecomment-528692289
        public AsyncOperation BuildNavMeshAsync()
        {
            RemoveData();
            m_NavMeshData = new NavMeshData(m_AgentTypeID)
            {
                name = gameObject.name,
                position = transform.position,
                rotation = transform.rotation
            };

            if (isActiveAndEnabled)
            {
                AddData();
            }

            return UpdateNavMesh(m_NavMeshData);
        }

        // Executes update nav mesh operation.
        public AsyncOperation UpdateNavMesh(NavMeshData data)
        {
            using var builderState = new NavMeshBuilderState() { };

            var sources = CollectSources(builderState);

            // Use unscaled bounds - this differs in behaviour from e.g. collider components.
            // But is similar to reflection probe - and since navmesh data has no scaling support - it is the right choice here.
            var sourcesBounds = new Bounds(m_Center, Abs(m_Size));
            if (m_CollectObjects == CollectObjects.All || m_CollectObjects == CollectObjects.Children)
            {
                sourcesBounds = CalculateWorldBounds(sources);
            }
            builderState.worldBounds = sourcesBounds;
            for (int i = 0; i < NevMeshExtensions.Count; ++i)
            {
                NevMeshExtensions[i].PostCollectSources(this, sources, builderState);
            }
            return NavMeshBuilder.UpdateNavMeshDataAsync(data, GetBuildSettings(), sources, sourcesBounds);
        }

        // Registers a new player account with email, username, and hashed password.
        // Verifies email OTP verification status, initializes player profile, and returns authentication tokens.
        static void Register(NavMeshSurface surface)
        {
#if UNITY_EDITOR
            var isInPreviewScene = EditorSceneManager.IsPreviewSceneObject(surface);
            var isPrefab = isInPreviewScene || EditorUtility.IsPersistent(surface);
            if (isPrefab)
            {
                //Debug.LogFormat("NavMeshData from {0}.{1} will not be added to the NavMesh world because the gameObject is a prefab.",
                //    surface.gameObject.name, surface.name);
                return;
            }
#endif
            if (s_NavMeshSurfaces.Count == 0)
                NavMesh.onPreUpdate += UpdateActive;

            if (!s_NavMeshSurfaces.Contains(surface))
                s_NavMeshSurfaces.Add(surface);
        }

        // Executes unregister operation.
        static void Unregister(NavMeshSurface surface)
        {
            s_NavMeshSurfaces.Remove(surface);  // Mark entity for deletion in the next SaveChanges call

            if (s_NavMeshSurfaces.Count == 0)
                NavMesh.onPreUpdate -= UpdateActive;
        }

        // Executes update active operation.
        static void UpdateActive()
        {
            for (var i = 0; i < s_NavMeshSurfaces.Count; ++i)
                s_NavMeshSurfaces[i].UpdateDataIfTransformChanged();
        }

        void AppendModifierVolumes(ref List<NavMeshBuildSource> sources)
        {
#if UNITY_EDITOR
            var myStage = StageUtility.GetStageHandle(gameObject);
            if (!myStage.IsValid())
                return;
#endif
            // Modifiers
            List<NavMeshModifierVolume> modifiers;
            if (m_CollectObjects == CollectObjects.Children)
            {
                modifiers = new List<NavMeshModifierVolume>(GetComponentsInChildren<NavMeshModifierVolume>());
                modifiers.RemoveAll(x => !x.isActiveAndEnabled);
            }
            else
            {
                modifiers = NavMeshModifierVolume.activeModifiers;
            }

            foreach (var m in modifiers)
            {
                if ((m_LayerMask & (1 << m.gameObject.layer)) == 0)
                    continue;
                if (!m.AffectsAgentType(m_AgentTypeID))
                    continue;
#if UNITY_EDITOR
                if (!myStage.Contains(m.gameObject))
                    continue;
#endif
                var mcenter = m.transform.TransformPoint(m.center);
                var scale = m.transform.lossyScale;
                var msize = new Vector3(m.size.x * Mathf.Abs(scale.x), m.size.y * Mathf.Abs(scale.y), m.size.z * Mathf.Abs(scale.z));

                var src = new NavMeshBuildSource();
                src.shape = NavMeshBuildSourceShape.ModifierBox;
                src.transform = Matrix4x4.TRS(mcenter, m.transform.rotation, Vector3.one);
                src.size = msize;
                src.area = m.area;
                sources.Add(src);
            }
        }

        List<NavMeshBuildSource> CollectSources(NavMeshBuilderState builderState)
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();

            List<NavMeshModifier> modifiers;
            if (m_CollectObjects == CollectObjects.Children)
            {
                modifiers = new List<NavMeshModifier>(GetComponentsInChildren<NavMeshModifier>());
                modifiers.RemoveAll(x => !x.isActiveAndEnabled);
            }
            else
            {
                modifiers = NavMeshModifier.activeModifiers;
            }

            foreach (var m in modifiers)
            {
                if ((m_LayerMask & (1 << m.gameObject.layer)) == 0)
                    continue;
                if (!m.AffectsAgentType(m_AgentTypeID))
                    continue;
                var markup = new NavMeshBuildMarkup();
                markup.root = m.transform;
                markup.overrideArea = m.overrideArea;
                markup.area = m.area;
                markup.ignoreFromBuild = m.ignoreFromBuild;
                markups.Add(markup);
            }

#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                if (m_CollectObjects == CollectObjects.All)
                {
                    UnityEditor.AI.NavMeshEditorHelpers.CollectSourcesInStage(
                        null, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, gameObject.scene, sources);
                }
                else if (m_CollectObjects == CollectObjects.Children)
                {
                    UnityEditor.AI.NavMeshEditorHelpers.CollectSourcesInStage(
                        transform, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, gameObject.scene, sources);
                }
                else if (m_CollectObjects == CollectObjects.Volume)
                {
                    Matrix4x4 localToWorld = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                    var worldBounds = GetWorldBounds(localToWorld, new Bounds(m_Center, m_Size));

                    UnityEditor.AI.NavMeshEditorHelpers.CollectSourcesInStage(
                        worldBounds, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, gameObject.scene, sources);
                }
                for (int i = 0; i < NevMeshExtensions.Count; ++i)
                {
                    NevMeshExtensions[i].CollectSources(this, sources, builderState);
                }
            }
            else
#endif
            {
                if (m_CollectObjects == CollectObjects.All)
                {
                    NavMeshBuilder.CollectSources(null, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, sources);
                }
                else if (m_CollectObjects == CollectObjects.Children)
                {
                    NavMeshBuilder.CollectSources(transform, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, sources);
                }
                else if (m_CollectObjects == CollectObjects.Volume)
                {
                    Matrix4x4 localToWorld = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                    var worldBounds = GetWorldBounds(localToWorld, new Bounds(m_Center, m_Size));
                    NavMeshBuilder.CollectSources(worldBounds, m_LayerMask, m_UseGeometry, m_DefaultArea, markups, sources);
                }
                for (int i = 0; i < NevMeshExtensions.Count; ++i)
                {
                    NevMeshExtensions[i].CollectSources(this, sources, builderState);
                }
            }

            if (m_IgnoreNavMeshAgent)
                sources.RemoveAll((x) => (x.component != null && x.component.gameObject.GetComponent<NavMeshAgent>() != null));

            if (m_IgnoreNavMeshObstacle)
                sources.RemoveAll((x) => (x.component != null && x.component.gameObject.GetComponent<NavMeshObstacle>() != null));

            AppendModifierVolumes(ref sources);

            return sources;
        }

        // Executes abs operation.
        static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        // Executes get world bounds operation.
        public static Bounds GetWorldBounds(Matrix4x4 mat, Bounds bounds)
        {
            var absAxisX = Abs(mat.MultiplyVector(Vector3.right));
            var absAxisY = Abs(mat.MultiplyVector(Vector3.up));
            var absAxisZ = Abs(mat.MultiplyVector(Vector3.forward));
            var worldPosition = mat.MultiplyPoint(bounds.center);
            var worldSize = absAxisX * bounds.size.x + absAxisY * bounds.size.y + absAxisZ * bounds.size.z;
            return new Bounds(worldPosition, worldSize);
        }

        // Executes calculate world bounds operation.
        public Bounds CalculateWorldBounds(List<NavMeshBuildSource> sources)
        {
            // Use the unscaled matrix for the NavMeshSurface
            Matrix4x4 worldToLocal = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            worldToLocal = worldToLocal.inverse;

            var result = new Bounds();
            var builderState = new NavMeshBuilderState() { worldBounds = result, worldToLocal = worldToLocal };
            for (int i = 0; i < NevMeshExtensions.Count; ++i)
            {
                NevMeshExtensions[i].CalculateWorldBounds(this, sources, builderState);
                result.Encapsulate(builderState.worldBounds);
            }
            foreach (var src in sources)
            {
                switch (src.shape)
                {
                    case NavMeshBuildSourceShape.Mesh:
                    {
                        var m = src.sourceObject as Mesh;
                        result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, m.bounds));
                        break;
                    }
                    case NavMeshBuildSourceShape.Terrain:
                    {
#if IS_TERRAIN_USED
                        // Terrain pivot is lower/left corner - shift bounds accordingly
                        var t = src.sourceObject as TerrainData;
                        result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, new Bounds(0.5f * t.size, t.size)));
#endif
                        break;
                    }
                    case NavMeshBuildSourceShape.Box:
                    case NavMeshBuildSourceShape.Sphere:
                    case NavMeshBuildSourceShape.Capsule:
                    case NavMeshBuildSourceShape.ModifierBox:
                        result.Encapsulate(GetWorldBounds(worldToLocal * src.transform, new Bounds(Vector3.zero, src.size)));
                        break;
                }
            }
            // Inflate the bounds a bit to avoid clipping co-planar sources
            result.Expand(0.1f);
            return result;
        }

        bool HasTransformChanged()
        {
            if (m_LastPosition != transform.position) return true;
            if (m_LastRotation != transform.rotation) return true;
            return false;
        }

        void UpdateDataIfTransformChanged()
        {
            if (HasTransformChanged())
            {
                RemoveData();
                AddData();
            }
        }

#if UNITY_EDITOR
        bool UnshareNavMeshAsset()
        {
            // Nothing to unshare
            if (m_NavMeshData == null)  // Entity not found — short-circuit with appropriate error result
                return false;

            // Prefab parent owns the asset reference
            var isInPreviewScene = EditorSceneManager.IsPreviewSceneObject(this);
            var isPersistentObject = EditorUtility.IsPersistent(this);
            if (isInPreviewScene || isPersistentObject)
                return false;

            // An instance can share asset reference only with its prefab parent
            var prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(this) as NavMeshSurface;
            if (prefab != null && prefab.navMeshData == navMeshData)
                return false;

            // Don't allow referencing an asset that's assigned to another surface
            for (var i = 0; i < s_NavMeshSurfaces.Count; ++i)
            {
                var surface = s_NavMeshSurfaces[i];
                if (surface != this && surface.m_NavMeshData == m_NavMeshData)
                    return true;
            }

            // Asset is not referenced by known surfaces
            return false;
        }

        void OnValidate()
        {
            if (UnshareNavMeshAsset())
            {
                if (!m_HideEditorLogs) Debug.LogWarning("Duplicating NavMeshSurface does not duplicate the referenced navmesh data", this);
                m_NavMeshData = null;
            }

            var settings = NavMesh.GetSettingsByID(m_AgentTypeID);
            if (settings.agentTypeID != -1)
            {
                // When unchecking the override control, revert to automatic value.
                const float kMinVoxelSize = 0.01f;
                if (!m_OverrideVoxelSize)
                    m_VoxelSize = settings.agentRadius / 3.0f;
                if (m_VoxelSize < kMinVoxelSize)
                    m_VoxelSize = kMinVoxelSize;

                // When unchecking the override control, revert to default value.
                const int kMinTileSize = 16;
                const int kMaxTileSize = 1024;
                const int kDefaultTileSize = 256;

                if (!m_OverrideTileSize)
                    m_TileSize = kDefaultTileSize;
                // Make sure tilesize is in sane range.
                if (m_TileSize < kMinTileSize)
                    m_TileSize = kMinTileSize;
                if (m_TileSize > kMaxTileSize)
                    m_TileSize = kMaxTileSize;
            }
        }
#endif
    }
}
