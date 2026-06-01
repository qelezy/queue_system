namespace WebApplication.Models.Configuration;

public sealed class AppSettingsOptions
{
    public const string SectionName = "AppSettings";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}
