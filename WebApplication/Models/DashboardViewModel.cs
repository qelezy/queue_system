namespace WebApplication.Models;

public class DashboardViewModel
{
    public int WaitingCount { get; set; }
    public int InServiceCount { get; set; }
    public int AvgWaitMinutes { get; set; }
    public int AvgServiceMinutes { get; set; }

    public IReadOnlyList<string> DailyLabels { get; set; } = [];
    public IReadOnlyList<int> DailyWaitMinutes { get; set; } = [];
    public IReadOnlyList<int> DailyServiceMinutes { get; set; } = [];

    public IReadOnlyList<CabinetLoad> Cabinets { get; set; } = [];
}

public record CabinetLoad(string Name, int LoadPercent);
