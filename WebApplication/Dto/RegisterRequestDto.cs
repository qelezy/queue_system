namespace MyWebApplication.Dto
{
    public class RegisterRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
    }
}
