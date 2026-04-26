namespace MyWebApplication.Dto
{
    public class PasswordResetTokenRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public string PasswordResetToken { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
