using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Dashboard;
using Xunit;

namespace WebApplication.Tests.Dashboard;

public sealed class QueueDashboardElapsedMinutesTests
{
    private static readonly DateTime From = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void ElapsedWholeMinutes_zero_when_not_started()
    {
        Assert.Equal(0, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(From, From));
        Assert.Equal(0, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(From, From.AddSeconds(-5)));
    }

    [Fact]
    public void ElapsedWholeMinutes_under_one_minute_is_zero()
    {
        Assert.Equal(0, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(From, From.AddSeconds(1)));
        Assert.Equal(0, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(From, From.AddSeconds(30)));
        Assert.Equal(0, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(From, From.AddSeconds(59)));
    }

    [Fact]
    public void ElapsedWholeMinutes_sixty_seconds_is_one_minute()
    {
        Assert.Equal(1, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(From, From.AddSeconds(60)));
    }

    [Fact]
    public void ElapsedWholeMinutes_ninety_seconds_is_one_minute()
    {
        Assert.Equal(1, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(From, From.AddSeconds(90)));
    }

    [Fact]
    public void CombineOnArrivalDate_uses_unspecified_wall_clock()
    {
        var combined = EqDateTimeExtensions.CombineOnArrivalDate(
            new DateOnly(2026, 5, 6),
            new TimeOnly(9, 0, 0));
        Assert.Equal(DateTimeKind.Unspecified, combined.Kind);
        var now = new DateTime(2026, 5, 6, 10, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(60, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(combined, now));
    }

    [Fact]
    public void ElapsedWholeMinutes_wall_clock_14_00_to_14_30()
    {
        var start = EqDateTimeExtensions.CombineOnArrivalDate(
            new DateOnly(2026, 5, 6),
            new TimeOnly(14, 0, 0));
        var now = new DateTime(2026, 5, 6, 14, 30, 0, DateTimeKind.Unspecified);
        Assert.Equal(30, QueueDashboardElapsedMinutes.ElapsedWholeMinutes(start, now));
    }
}
