using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Executes scriptable object operation.
[CreateAssetMenu(fileName = "MonsterDatabase", menuName = "ScriptableObjects/Monster Database")]
public class MonsterDatabaseSO : ScriptableObject
{
    public List<MonsterClientData> allMonsters = new List<MonsterClientData>();

    // Executes get monster data operation.
    public MonsterClientData GetMonsterData(int id)
    {
        return allMonsters.Find(m => m != null && m.MonsterId == id);
    }

#if UNITY_EDITOR
    [ContextMenu("Load All Monsters In Project")]
    // Executes load all monsters in project operation.
    public void LoadAllMonstersInProject()
    {
        allMonsters.Clear();
        string[] guids = AssetDatabase.FindAssets("t:MonsterClientData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonsterClientData monster = AssetDatabase.LoadAssetAtPath<MonsterClientData>(path);
            if (monster != null && !allMonsters.Contains(monster))
            {
                allMonsters.Add(monster);
            }
        }

        allMonsters.Sort((a, b) => a.MonsterId.CompareTo(b.MonsterId));

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green>[MonsterDatabaseSO] Đã load thành công {allMonsters.Count} quái vào Database!</color>");
    }
#endif
}
