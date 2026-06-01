using WebApplication.Models.Configuration;

namespace WebApplication.Configuration;

public static class JwtConfiguration
{
    public static string GetRequiredSigningKey(AppSettingsOptions appSettings)
    {
        if (string.IsNullOrWhiteSpace(appSettings.Token))
        {
            throw new InvalidOperationException(
                "AppSettings:Token не задан. Укажите AppSettings__Token в .env или User Secrets.");
        }

        return appSettings.Token;
    }
}
