#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Initializes a new default instance of the IceFairySetupUtility class.
public static class IceFairySetupUtility
{
    [MenuItem("Tools/Fix IceFairy Components")]
    // Executes fix ice fairy operation.
    public static void FixIceFairy()
    {
        GameObject[] gameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = 0;

        GameObject sceneGolem = GameObject.Find("Golem Boss") ?? GameObject.Find("GolemBoss") ?? GameObject.Find("Golem");

        foreach (var go in gameObjects)
        {
            if (go == null) continue;

            string cleanName = go.name.Replace(" ", "").Replace("(Clone)", "");
            if (cleanName.Equals("IceFairy", System.StringComparison.OrdinalIgnoreCase))
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Fix IceFairy Components");

                var entity = go.GetComponent<EnemyEntity>();
                if (entity == null)
                {
                    entity = go.AddComponent<EnemyEntity>();
                    Debug.Log($"[IceFairySetupUtility] Added missing EnemyEntity to '{go.name}'");
                }
                entity.SetSpawnData(21, 0);


                var supportAI = go.GetComponent<IceFairySupportAI>();
                if (supportAI == null)
                {
                    supportAI = go.AddComponent<IceFairySupportAI>();
                    Debug.Log($"[IceFairySetupUtility] Added missing IceFairySupportAI to '{go.name}'");
                }

                if (sceneGolem != null)
                {
                    var serializedAI = new SerializedObject(supportAI);
                    var bossProp = serializedAI.FindProperty("targetBossTransform");
                    if (bossProp != null)
                    {
                        bossProp.objectReferenceValue = sceneGolem.transform;
                        serializedAI.ApplyModifiedProperties();
                    }

                    supportAI.SnapToBossPosition();
                }

                EditorUtility.SetDirty(go);
                count++;
            }
        }

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"<color=green>[IceFairySetupUtility] Successfully fixed {count} IceFairy instance(s) in current Scene.</color>");
        }
        else
        {
            Debug.LogWarning("[IceFairySetupUtility] No 'Ice Fairy' GameObject found in active Scene.");
        }
    }
}
#endif
