using System;

namespace MysticJourney.API.Models.Request
{
    // Initializes a new default instance of the GachaPullRequest class.
    [Serializable]
    public class GachaPullRequest
    {
        public int GachaBannerId;
        public int PullCount;
        public bool IsFreePull;
    }
}
