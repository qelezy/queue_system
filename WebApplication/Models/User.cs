using Microsoft.AspNetCore.Identity;

namespace WebApplication.Models
{
    public class User : IdentityUser
    {
        public string? RefreshToken = string.Empty;
        public DateTime? RefreshTokenExpireTime;
    }
}
