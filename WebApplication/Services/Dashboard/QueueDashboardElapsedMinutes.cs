namespace WebApplication.Services.Dashboard;

public static class QueueDashboardElapsedMinutes
{
    public static int ElapsedWholeMinutes(DateTime from, DateTime to)
    {
        var fromWall = ToWallClock(from);
        var toWall = ToWallClock(to);
        if (toWall <= fromWall)
            return 0;

        var seconds = (toWall - fromWall).TotalSeconds;
        if (seconds <= 0)
            return 0;

        return (int)Math.Floor(seconds / 60.0);
    }

    private static DateTime ToWallClock(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Unspecified => value,
            DateTimeKind.Utc => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        };
}
