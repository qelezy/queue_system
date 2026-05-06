using Microsoft.AspNetCore.Identity;

namespace WebApplication.Models
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Patronymic { get; set; }

        public string? RefreshToken { get; set; } = string.Empty;
        public DateTime? RefreshTokenExpiresAt { get; set; }

        public bool RefreshSessionExtended { get; set; }
    }
}
