namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.IMGUI.Controls;
  using UnityEngine;
  using Object = UnityEngine.Object;
  
#if UNITY_6000_2_OR_NEWER
  using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
  using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
  using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
#endif

  // Executes editor window operation.
  public class NetworkPrefabsInspector : EditorWindow {

    private Grid _grid = new Grid();
    
    // Executes show window operation.
    [MenuItem("Tools/Fusion/Windows/Network Prefabs Inspector")]
    [MenuItem("Window/Fusion/Network Prefabs Inspector")]
    public static void ShowWindow() {
      var window = GetWindow<NetworkPrefabsInspector>(false, "Network Prefabs Inspector");
      window.Show();
    }
    
    // Callback invoked when NetworkPrefabsInspector becomes enabled and active in the scene hierarchy.
    // Subscribes to global game events and refreshes visible UI displays.
    private void OnEnable() {
      _grid.PrefabTable = NetworkProjectConfig.Global.PrefabTable;
      _grid.OnEnable();
    }

    // Executes on inspector update operation.
    private void OnInspectorUpdate() {
      _grid.OnInspectorUpdate();
    }

    // Executes on gui operation.
    private void OnGUI() {
      using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
        _grid.DrawToolbarReloadButton();
        _grid.DrawToolbarSyncSelectionButton();
        GUILayout.FlexibleSpace();
        
        EditorGUI.BeginChangeCheck();
        _grid.OnlyLoaded = GUILayout.Toggle(_grid.OnlyLoaded, "Loaded Only", EditorStyles.toolbarButton);
        if (EditorGUI.EndChangeCheck()) {
          _grid.ResetTree();
        }

        _grid.DrawToolbarSearchField();
      }

      var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
      _grid.OnGUI(rect);
    }
    

    // Executes load state operation.
    private enum LoadState {
      NotLoaded,
      Loading,
      LoadedNoInstances,
      Loaded
    }

    // Executes tree view state operation.
    [Serializable]
    private class InspectorTreeViewState : TreeViewState {
      public MultiColumnHeaderState HeaderState;
      public bool                   SyncSelection;
    }

    // Executes fusion grid item operation.
    private class GridItem : FusionGridItem {
      private readonly NetworkPrefabId    _prefabId;
      private readonly NetworkPrefabTable _prefabTable;

      // Executes grid item operation.
      public GridItem(NetworkPrefabTable prefabTable, NetworkPrefabId prefabId) {
        _prefabId = prefabId;
        _prefabTable = prefabTable;
      }
      
      // Executes instance count operation.
      public int InstanceCount => _prefabTable.GetInstancesCount(_prefabId);

      // Executes path operation.
      public string Path => AssetDatabase.GUIDToAssetPath(Guid);

      // Executes guid operation.
      public string Guid => Source?.AssetGuid.ToUnityGuidString() ?? "Null";

      // Executes target object operation.
      public override Object TargetObject {
        get {
          if (Source?.AssetGuid.IsValid == true) {
            if (NetworkProjectConfigUtilities.TryGetPrefabEditorInstance(Source.AssetGuid, out var result)) {
              return result.gameObject;
            }
          }

          return null;
        }
      }

      // Executes source operation.
      public INetworkPrefabSource Source => _prefabTable.GetSource(_prefabId);

      // Executes description operation.
      public string Description {
        get => Source?.Description ?? "Null";
      }

      // Executes load state operation.
      public LoadState LoadState {
        get {
          if (!_prefabTable.IsAcquired(_prefabId)) {
            return LoadState.NotLoaded;
          }

          if (!_prefabTable.GetSource(_prefabId).IsCompleted) {
            return LoadState.Loading;
          }
          
          if (_prefabTable.GetInstancesCount(_prefabId) == 0) {
            return LoadState.LoadedNoInstances;
          }

          return LoadState.Loaded;
        }
      }

      // Executes prefab id operation.
      public NetworkPrefabId PrefabId => _prefabId;
    }

    [Serializable]
    class Grid : FusionGrid<GridItem> {

      [SerializeField]
      public NetworkPrefabTable PrefabTable;
      [SerializeField]
      public bool OnlyLoaded;

      // Executes get content hash operation.
      public override int GetContentHash() {
        return PrefabTable?.Version ?? 0;
      }

      // Executes create columns operation.
      protected override IEnumerable<Column> CreateColumns() {
        yield return new() {
          headerContent = new GUIContent("State"),
          width = 40,
          autoResize = false,
          cellGUI = (item, rect, _, _) => {
            var icon = FusionEditorSkin.LoadStateIcon;
            string label = "";
            Color color;
            switch (item.LoadState) {
              case LoadState.Loaded:
                color = Color.green;
                label = item.InstanceCount.ToString();
                break;
              case LoadState.LoadedNoInstances:
                color = Color.yellow;
                label = "0";
                break;
              case LoadState.Loading:
                color = Color.yellow;
                color.a = 0.5f;
                label = "0";
                break;
              default:
                color = Color.gray;
                break;
            }

            using (new FusionEditorGUI.ContentColorScope(color)) {
              EditorGUI.LabelField(rect, new GUIContent(label, icon, item.LoadState.ToString()));
            }
          },
          getComparer = order => (a, b) => {
            var result = a.LoadState.CompareTo(b.LoadState) * order;
            if (result != 0) {
              return result;
            }
            return a.InstanceCount.CompareTo(b.InstanceCount) * order;
          },
        };
        yield return new() {
          headerContent = new GUIContent("Type"),
          width = 40,
          maxWidth = 40,
          minWidth = 40,
          cellGUI = (item, rect, _, _) => INetworkPrefabSourceDrawer.DrawThumbnail(rect, item.Source),
          getComparer = order => (a, b) => EditorUtility.NaturalCompare(a.Source?.GetType().Name ?? "", b.Source?.GetType().Name ?? "") * order,
        };
        yield return MakeSimpleColumn(x => x.PrefabId, new() {
          cellGUI = (item, rect, selected, focused) => TreeView.DefaultGUI.Label(rect, item.PrefabId.ToString(false, false), selected , focused),
          width = 50,
          autoResize = false
        });
        yield return MakeSimpleColumn(x => x.Path, new() {
          initiallySorted = true,
        });
        yield return MakeSimpleColumn(x => x.Guid, new() {
          initiallyVisible = false
        });
        yield return MakeSimpleColumn(x => x.Description, new() {
          initiallyVisible = false
        });
      }

      // Executes create rows operation.
      protected override IEnumerable<GridItem> CreateRows() {
        if (PrefabTable == null) {  // Entity not found — short-circuit with appropriate error result
          yield break;
        }

        for (int i = 0; i < PrefabTable.Prefabs.Count; ++i) {
          var prefabId = NetworkPrefabId.FromIndex(i);
          if (OnlyLoaded && !PrefabTable.IsAcquired(prefabId)) {
            continue;
          }
          yield return new GridItem(PrefabTable, NetworkPrefabId.FromIndex(i)) { id = (int)(i + 1) };
        }
      }

      // Executes create context menu operation.
      protected override GenericMenu CreateContextMenu(GridItem item, TreeView treeView) {
        
        var menu = new GenericMenu();

        var selection = treeView.GetSelection()
         .Select(x => NetworkPrefabId.FromIndex(x-1))
         .ToList();

        var anyLoaded = selection.Any(x => PrefabTable.IsAcquired(x));
        var anyNotLoaded = selection.Any(x => !PrefabTable.IsAcquired(x));
        var anyInstances = selection.Any(x => PrefabTable.GetInstancesCount(x) > 0);
        var spawnerRunners = NetworkRunner.Instances.Where(x => x && x.IsRunning && x.CanSpawn).ToArray();  // Filter records matching the predicate
        
        var loadContent = new GUIContent("Load");
        var loadAsyncContent = new GUIContent("Load (async)");
        var unloadContent = new GUIContent("Unload");
        var selectInstancesContent = new GUIContent("Select Instances");
        var spawnContent = new GUIContent("Spawn");
        var spawnAsyncContent = new GUIContent("Spawn (async)");

        if (anyNotLoaded) {
          menu.AddItem(loadContent, false, () => {
            foreach (var id in selection) {
              PrefabTable.Load(id, isSynchronous: true);
            }
          });
          menu.AddItem(loadAsyncContent, false, () => {
            foreach (var id in selection) {
              PrefabTable.Load(id, isSynchronous: false);
            }
          });
        } else {
          menu.AddDisabledItem(loadContent);
          menu.AddDisabledItem(loadAsyncContent);
        }
        
        if (anyLoaded) {
          menu.AddItem(unloadContent, false, () => {
            foreach (var id in selection) {
              PrefabTable.Unload(id);
            }
          });
        } else {
          menu.AddDisabledItem(unloadContent);
        }

        if (anyInstances) {
          menu.AddItem(selectInstancesContent, false, () => {
            var lookup = new HashSet<NetworkObjectTypeId>(selection.Select(x => NetworkObjectTypeId.FromPrefabId(x)));
            Selection.objects = FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
             .Where(x => x.NetworkTypeId.IsValid && lookup.Contains(x.NetworkTypeId))  // Filter records matching the predicate
             .Select(x => x.gameObject)
             .ToArray();
          });
        } else {
          menu.AddDisabledItem(selectInstancesContent);
        }

        menu.AddSeparator("");
        
        if (spawnerRunners.Any()) {
          if (spawnerRunners.Length > 1) {
            foreach (var runner in spawnerRunners.Where(x => x.CanSpawn)) {  // Filter records matching the predicate
              AddSpawnItems($"/{runner.name}", runner);
            }
          } else {
            AddSpawnItems($"", spawnerRunners[0]);
          }
        } else {
          menu.AddDisabledItem(spawnContent);
          menu.AddDisabledItem(spawnAsyncContent);
        }

        void AddSpawnItems(string s, NetworkRunner networkRunner) {
          menu.AddItem(new GUIContent($"{spawnContent.text}{s}"), false, () => {
            foreach (var id in selection) {
              networkRunner.TrySpawn(id, out _);
            }
          });
          menu.AddItem(new GUIContent($"{spawnAsyncContent.text}{s}"), false, () => {
            foreach (var id in selection) {
              networkRunner.SpawnAsync(id);
            }
          });
        }

        return menu;
      }
    }
  }
}
