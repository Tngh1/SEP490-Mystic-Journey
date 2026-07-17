using System;
using System.Collections.Generic;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Models.Request
{
    [Serializable]
    public class UpdatePlayerBuffsRequest
    {
        public List<PlayerBuffDTO> Buffs = new List<PlayerBuffDTO>();
    }
}
