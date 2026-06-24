using System;

namespace MysticJourney.API.Models.Request
{
    [Serializable]
    public class MonsterDefeatRequest
    {
        public int? MonsterSpawnId { get; set; }
        public int? DungeonSessionId { get; set; }
    }
}
