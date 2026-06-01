namespace WebApplication.Models.Configuration;

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    public int QueueAvailabilityCacheSeconds { get; set; } = 30;

    public int DashboardRefreshSeconds { get; set; } = 10;

    public string QueueTimeZoneId { get; set; } = "Russian Standard Time";

    public List<MonitoringPermissionOptions> Permissions { get; set; } = new();
}
