using UnityEngine;

// Executes scriptable object operation.
[CreateAssetMenu(fileName = "NewMonsterClientData", menuName = "ScriptableObjects/Monster Client Data")]
public class MonsterClientData : ScriptableObject
{
    [Header("Database Mapping")]
    public int MonsterId;

    [Header("Visual & Client Assets")]
    public Sprite MonsterIcon;
    public GameObject MonsterPrefab;
}
