using System;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the ClassConfigDTO class.
    [Serializable]
    public class ClassConfigDTO
    {
        public int ClassConfigId;
        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        public string ClassName;
        public int MaxHp;
        public int Atk;
        public int Def;
        public int MoveSpeed;
        public int AttackSpeed;
        public int CritRate;
        public int CritDamage;
        public int DamageBonus;
    }
}
