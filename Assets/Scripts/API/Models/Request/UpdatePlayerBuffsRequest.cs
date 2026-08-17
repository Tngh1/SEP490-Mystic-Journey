using System;
using System.Collections.Generic;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Models.Request
{
    // Initializes a new default instance of the UpdatePlayerBuffsRequest class.
    [Serializable]
    public class UpdatePlayerBuffsRequest
    {
        public List<PlayerBuffDTO> Buffs = new List<PlayerBuffDTO>();
    }
}
