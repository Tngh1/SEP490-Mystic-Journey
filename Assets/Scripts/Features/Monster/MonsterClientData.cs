using UnityEngine;

[CreateAssetMenu(fileName = "NewMonsterClientData", menuName = "ScriptableObjects/Monster Client Data")]
public class MonsterClientData : ScriptableObject
{
    [Header("Database Mapping")]
    public int MonsterId; // Trùng với ID trên Database Backend (Ví dụ: 1)

    [Header("Visual & Client Assets")]
    public Sprite MonsterIcon;    // Hình ảnh sẽ hiện trong UI Bestiary
    public GameObject MonsterPrefab; // Cục Prefab sẽ spawn ra ngoài Map để đánh nhau
}