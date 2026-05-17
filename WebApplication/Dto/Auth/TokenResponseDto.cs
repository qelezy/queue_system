namespace WebApplication.Dto.Auth {
    public class TokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
        public string Role { get; set; } = string.Empty;

        public bool RefreshSessionExtended { get; set; }
    }
}
