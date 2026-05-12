namespace WebApplication.Services.Reports.Intervals;

/// <summary>Полуоткрытый интервал не используем: [Start, End) — оба конца включены для простоты с TimeOnly из БД.</summary>
public readonly record struct DateTimeInterval(DateTime Start, DateTime End)
{
    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;

    public bool IsEmptyOrInverted => Start >= End;
}
