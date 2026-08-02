namespace MysticJourney.API.Models.Request
{
    // POST /api/accounts/login-game
    [System.Serializable]
    public class LoginGameRequest
    {
        public string EmailOrUsername { get; set; }
        public string Password { get; set; }

        // Server phân biệt client game với web admin portal qua trường này: chỉ client game
        // bị chặn khi tài khoản đang được chơi ở máy khác, và chỉ client game ghi mốc online.
        public string ClientType { get; set; } = "Game";
    }
}

namespace MysticJourney.API.Models.Response
{
    // Response: POST /api/accounts/login-game
    [System.Serializable]
    public class LoginGameResponse
    {
        public int AccountId { get; set; }
        public string UserName { get; set; }
        public string EmailAddress { get; set; }
        public int RoleId { get; set; }
        public int? PlayerProfileId { get; set; }      // null nếu chưa tạo profile
        public string PlayerDisplayName { get; set; }
        public string PlayerClass { get; set; }
        public int Level { get; set; }
        public string LastMapName { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public string AccessToken { get; set; }
        public string AccessTokenExpiresAt { get; set; }
        public string RefreshToken { get; set; }
        public string RefreshTokenExpiresAt { get; set; }
    }

    // Response: GET /api/accounts/me  (cần auth)
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
