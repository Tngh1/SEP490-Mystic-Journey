using UnityEngine;

[CreateAssetMenu(fileName = "New Skill", menuName = "Skill System/Skill")]
public class SkillData : ScriptableObject
{
    [Header("Database Mapping")]
    public int skillId; // BẮT BUỘC TRÙNG VỚI SkillId TRONG POSTGRESQL

    [Header("Visual & Client Assets")]
    public Sprite skillIcon;
    public Sprite customBackground; // (Tùy chọn) Background riêng nếu muốn
    public GameObject skillPrefab; // Hiệu ứng tung chiêu
    [Header("Gameplay")]
    public string classRequirement = ""; // "Knight", "Archer", "Mage" - use empty or "All" for any class
}