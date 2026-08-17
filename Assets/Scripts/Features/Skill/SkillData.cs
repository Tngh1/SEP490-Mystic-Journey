using UnityEngine;

// Executes scriptable object operation.
[CreateAssetMenu(fileName = "New Skill", menuName = "Skill System/Skill")]
public class SkillData : ScriptableObject
{
    [Header("Database Mapping")]
    public int skillId;

    [Header("Visual & Client Assets")]
    public Sprite skillIcon;
    public Sprite customBackground;
    public GameObject skillPrefab;
    [Header("Gameplay")]
    // Supported class requirements: Knight, Archer, Mage, or All; All allows every player class to use the skill or reward.
    public string classRequirement = "";
}
