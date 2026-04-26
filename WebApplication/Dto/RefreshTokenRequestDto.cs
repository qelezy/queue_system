using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace MyWebApplication.Dto
{
    public class RefreshTokenRequestDto
    {
        public Guid UserId { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
    }
}
