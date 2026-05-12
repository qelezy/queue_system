namespace WebApplication.Models;

/// <summary>Какие блоки мониторинга показывать по permission_name.</summary>
public sealed class DashboardUiVisibility
{
    public bool WaitingCard { get; init; }
    public bool InServiceCard { get; init; }
    public bool AcceptedTodayCard { get; init; }
    public bool NoShowTodayCard { get; init; }
    public bool AvgWaitCard { get; init; }
    public bool AvgServiceCard { get; init; }
    public bool QueueTable { get; init; }
    public bool DoctorLoad { get; init; }
}
