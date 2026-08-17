using System;

namespace MysticJourney.API.Models.Request
{
    // Initializes a new default instance of the MonsterDefeatRequest class.
    [Serializable]
    public class MonsterDefeatRequest
    {
        // Executes monster spawn id operation.
        public int? MonsterSpawnId { get; set; }
        // Executes dungeon session id operation.
        public int? DungeonSessionId { get; set; }
    }
}
