using System;

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class PlayerBuffDTO
    {
        public string BuffName;
        public string IconName;
        public float DurationRemaining;
        public bool IsDebuff;
    }
}
