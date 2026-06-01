using WebApplication.Models.Configuration;

namespace WebApplication.Configuration;

public static class ConfigurationValidation
{
    public static void ValidateRequiredConfiguration(IConfiguration configuration)
    {
        var connections = configuration.GetSection(ConnectionStringsOptions.SectionName).Get<ConnectionStringsOptions>();
        if (connections is null)
        {
            throw new InvalidOperationException(
                "Секция ConnectionStrings не задана. Укажите ConnectionStrings__UserDatabase и ConnectionStrings__ElectronicQueue в .env.");
        }

        if (string.IsNullOrWhiteSpace(connections.UserDatabase))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:UserDatabase не задан. Укажите ConnectionStrings__UserDatabase в .env.");
        }

        if (string.IsNullOrWhiteSpace(connections.ElectronicQueue))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:ElectronicQueue не задан. Укажите ConnectionStrings__ElectronicQueue в .env.");
        }

        var appSettings = configuration.GetSection(AppSettingsOptions.SectionName).Get<AppSettingsOptions>();
        if (appSettings is null)
        {
            throw new InvalidOperationException(
                "Секция AppSettings не задана. Укажите AppSettings__Token, AppSettings__Issuer, AppSettings__Audience в .env.");
        }

        if (string.IsNullOrWhiteSpace(appSettings.Token))
        {
            throw new InvalidOperationException(
                "AppSettings:Token не задан. Укажите AppSettings__Token в .env или User Secrets.");
        }

        if (string.IsNullOrWhiteSpace(appSettings.Issuer))
        {
            throw new InvalidOperationException(
                "AppSettings:Issuer не задан. Укажите AppSettings__Issuer в .env.");
        }

        if (string.IsNullOrWhiteSpace(appSettings.Audience))
        {
            throw new InvalidOperationException(
                "AppSettings:Audience не задан. Укажите AppSettings__Audience в .env.");
        }

        var email = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>();
        if (email is null || string.IsNullOrWhiteSpace(email.From))
        {
            throw new InvalidOperationException(
                "Email:From не задан. Укажите Email__From в .env или задайте дефолт в EmailOptions.");
        }
    }
}
