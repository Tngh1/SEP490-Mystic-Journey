using UnityEngine;

[CreateAssetMenu(fileName = "New Skill", menuName = "Skill System/Skill")]
public class SkillData : ScriptableObject
{
    [Header("Database Mapping")]
    public int skillId; // BẮT BUỘC TRÙNG VỚI SkillId TRONG POSTGRESQL

    [Header("Visual & Client Assets")]
    public Sprite skillIcon;
    public GameObject skillPrefab; // Hiệu ứng tung chiêu
}