namespace WebApplication.Services.Reports.Intervals;

public readonly record struct DateTimeInterval(DateTime Start, DateTime End)
{
    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;

    public bool IsEmptyOrInverted => Start >= End;
}
