namespace MysticJourney.API.Models.Request
{
    [System.Serializable]
    public class LoginGameRequest
    {
        public string EmailOrUsername { get; set; }
        public string Password { get; set; }

        // Supported client types: Web or Game (selects independent refresh-token slot and session behavior)
        public string ClientType { get; set; } = "Game";
    }
}

namespace MysticJourney.API.Models.Response
{
    [System.Serializable]
    public class LoginGameResponse
    {
        public int AccountId { get; set; }
        public string UserName { get; set; }
        public string EmailAddress { get; set; }
        public int RoleId { get; set; }
        public bool HasCharacter { get; set; }
        public int? PlayerProfileId { get; set; }
        public string PlayerDisplayName { get; set; }
        public string PlayerClass { get; set; }
        public int Level { get; set; }
        public string LastMapName { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public string AccessToken { get; set; }
        public string AccessTokenExpiresAt { get; set; }
        // Refresh token rotated on each session exchange
        public string RefreshToken { get; set; }
        public string RefreshTokenExpiresAt { get; set; }
    }

    [System.Serializable]
    public class MeResponse
    {
        public int AccountId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public int? PlayerProfileId { get; set; }
        public string PlayerClass { get; set; }
        public int Level { get; set; }
        public string LastMapName { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }
}
