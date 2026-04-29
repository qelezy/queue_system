namespace WebApplication.Dto
{
    public class PasswordResetDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
