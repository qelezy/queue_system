namespace WebApplication.Services.Dashboard;

public sealed class DashboardSnapshotDto
{
    public bool IsDemoData { get; set; }

    public int WaitingCount { get; set; }
    public int InServiceCount { get; set; }
    public int AcceptedTodayCount { get; set; }
    public int AvgWaitMinutes { get; set; }
    public int MaxWaitMinutes { get; set; }
    public int AvgServiceMinutes { get; set; }
    public int MaxServiceMinutes { get; set; }

    public IReadOnlyList<DashboardQueueRowDto> ActiveQueue { get; set; } = [];
    public IReadOnlyList<DoctorLoadCardDto> DoctorLoadCards { get; set; } = [];
}

public sealed class DashboardQueueRowDto
{
    public int IdAppointment { get; set; }
    public string Patient { get; set; } = "";
    public int TicketPriority { get; set; }
    public int CategoryPriority { get; set; }
    public int WaitingMinutes { get; set; }
    public string CurrentCabinet { get; set; } = "";
    public string CurrentDoctor { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string ArrivalTime { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string StatusCode { get; set; } = "";
}

public sealed class DoctorLoadCardDto
{
    public int IdDoctor { get; set; }
    public string FullName { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string Cabinet { get; set; } = "";
    public bool IsInService { get; set; }
    public int? CurrentServiceMinutes { get; set; }
    public int? NormServiceMinutes { get; set; }
    public int QueueLength { get; set; }
}
