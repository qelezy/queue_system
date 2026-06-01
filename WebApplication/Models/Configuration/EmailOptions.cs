namespace WebApplication.Models.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 1025;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;
    public string From { get; set; } = "test@myapp.local";
    public bool EnableSsl { get; set; }
}
