using System;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the PlayerBuffDTO class.
    [Serializable]
    public class PlayerBuffDTO
    {
        public string BuffName;
        public string IconName;
        public float DurationRemaining;
        public bool IsDebuff;
    }
}
