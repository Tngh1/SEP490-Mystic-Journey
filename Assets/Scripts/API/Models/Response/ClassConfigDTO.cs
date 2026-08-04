using System;

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class ClassConfigDTO
    {
        public int ClassConfigId;
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
