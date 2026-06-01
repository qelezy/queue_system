using Microsoft.Extensions.Options;
using WebApplication.Models.Configuration;

namespace WebApplication.Services.Dashboard;

public sealed class QueueDashboardClock : IQueueDashboardClock
{
    private readonly TimeZoneInfo _timeZone;

    public QueueDashboardClock(IOptions<MonitoringOptions> options)
    {
        var id = string.IsNullOrWhiteSpace(options.Value.QueueTimeZoneId)
            ? "Russian Standard Time"
            : options.Value.QueueTimeZoneId.Trim();
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
    }

    public DateOnly TodayDateOnly() => DateOnly.FromDateTime(Now().Date);

    public DateTime Now()
    {
        var zoned = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
        return DateTime.SpecifyKind(zoned, DateTimeKind.Unspecified);
    }
}
