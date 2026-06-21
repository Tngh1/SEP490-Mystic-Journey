using UnityEngine;

[CreateAssetMenu(fileName = "New Skill", menuName = "Skill System/Skill")]
public class SkillData : ScriptableObject
{
    [Header("UI Info")]
    public string skillName;
    [TextArea] public string description;
    public Sprite skillIcon; // Bạn kéo ảnh từ thư mục Sprites vào đây để UI hiển thị

    [Header("Gameplay Info")]
    public int unlockLevel;
    public float manaCost;

    // ĐÂY LÀ PHẦN THÊM VÀO:
    public GameObject skillPrefab; // Bạn kéo cục "knight_attack" từ thư mục Prefabs vào đây
}