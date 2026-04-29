namespace WebApplication.Models
{
    public class JwtOptions
    {
        public int AccessTokenMinutes { get; set; } = 30;
        public int RefreshRememberDays { get; set; } = 30;
        public int RefreshSessionHours { get; set; } = 8;
    }
}
