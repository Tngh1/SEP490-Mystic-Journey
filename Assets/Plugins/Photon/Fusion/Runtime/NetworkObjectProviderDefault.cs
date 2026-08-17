namespace Fusion {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using UnityEngine.Serialization;

  // Executes i network object provider operation.
  public class NetworkObjectProviderDefault : Fusion.Behaviour, INetworkObjectProvider {
    /// <summary>
    /// If enabled, the provider will delay acquiring a prefab instance if the scene manager is busy.
    /// </summary>
    [InlineHelp]
    public bool DelayIfSceneManagerIsBusy = true;

    // Executes acquire prefab instance operation.
    public virtual NetworkObjectAcquireResult AcquirePrefabInstance(NetworkRunner runner, in NetworkPrefabAcquireContext context, out NetworkObject instance) {

      instance = null;

      if (DelayIfSceneManagerIsBusy && runner.SceneManager.IsBusy) {
        return NetworkObjectAcquireResult.Retry;
      }

      NetworkObject prefab;
      try {
        prefab = runner.Prefabs.Load(context.PrefabId, isSynchronous: context.IsSynchronous);
      } catch (Exception ex) {
        Log.Error($"Failed to load prefab: {ex}");
        return NetworkObjectAcquireResult.Failed;
      }

      if (!prefab) {
        // this is ok, as long as Fusion does not require the prefab to be loaded immediately;
        // if an instance for this prefab is still needed, this method will be called again next update
        return NetworkObjectAcquireResult.Retry;
      }

      instance = InstantiatePrefab(runner, prefab);
      Assert.Check(instance);

      if (context.DontDestroyOnLoad) {
        runner.MakeDontDestroyOnLoad(instance.gameObject);
      } else {
        runner.MoveToRunnerScene(instance.gameObject);
      }

      runner.Prefabs.AddInstance(context.PrefabId);
      return NetworkObjectAcquireResult.Success;
    }

    // Executes release instance operation.
    // Throws an exception if precondition validations fail.
    public virtual void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context) {
      var instance = context.Object;

      if (!context.IsBeingDestroyed) {
        if (context.TypeId.IsPrefab) {
          DestroyPrefabInstance(runner, context.TypeId.AsPrefabId, instance);
        } else if (context.TypeId.IsSceneObject) {
          DestroySceneObject(runner, context.TypeId.AsSceneObjectId, instance);
        } else if (context.IsNestedObject) {
          DestroyPrefabNestedObject(runner, instance);
        } else {
          throw new NotImplementedException($"Unknown type id {context.TypeId}");
        }
      }

      if (context.TypeId.IsPrefab) {
        runner.Prefabs.RemoveInstance(context.TypeId.AsPrefabId);
      }
    }

    // Executes get prefab id operation.
    public NetworkPrefabId GetPrefabId(NetworkRunner runner, NetworkObjectGuid prefabGuid) {
      return runner.Prefabs.GetId(prefabGuid);
    }

    // Executes instantiate prefab operation.
    protected virtual NetworkObject InstantiatePrefab(NetworkRunner runner, NetworkObject prefab) {
      return Instantiate(prefab);
    }

    // Executes destroy prefab instance operation.
    protected virtual void DestroyPrefabInstance(NetworkRunner runner, NetworkPrefabId prefabId, NetworkObject instance) {
      Destroy(instance.gameObject);
    }
    
    // Executes destroy prefab nested object operation.
    protected virtual void DestroyPrefabNestedObject(NetworkRunner runner, NetworkObject instance) {
      Destroy(instance.gameObject);
    }

    // Executes destroy scene object operation.
    protected virtual void DestroySceneObject(NetworkRunner runner, NetworkSceneObjectId sceneObjectId, NetworkObject instance) {
      Destroy(instance.gameObject);
    }
    
    void INetworkObjectProvider.Shutdown(NetworkRunner runner) {
      var prefabs = runner.Prefabs;
      if (prefabs?.Options.UnloadUnusedPrefabsOnShutdown == true) {
        prefabs.UnloadUnreferenced(includeIncompleteLoads: true);
      }
    }
  }
}