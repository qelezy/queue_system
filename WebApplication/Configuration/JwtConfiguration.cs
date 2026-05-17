namespace WebApplication.Configuration;

public static class JwtConfiguration
{
    public static string GetRequiredSigningKey(IConfiguration configuration)
    {
        var token = configuration["AppSettings:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "AppSettings:Token не задан. Укажите JWT signing key в .env или User Secrets (AppSettings__Token).");
        }

        return token;
    }
}
