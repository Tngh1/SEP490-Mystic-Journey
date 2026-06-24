using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterDatabase", menuName = "ScriptableObjects/Monster Database")]
public class MonsterDatabaseSO : ScriptableObject
{
    public List<MonsterClientData> allMonsters;

    // Hàm tiện ích để lấy hình ảnh/prefab dựa vào ID
    public MonsterClientData GetMonsterData(int id)
    {
        return allMonsters.Find(m => m.MonsterId == id);
    }
}