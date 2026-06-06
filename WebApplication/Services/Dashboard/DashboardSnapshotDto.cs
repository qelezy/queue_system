namespace WebApplication.Services.Dashboard;

public sealed class DashboardSnapshotDto
{
    public int WaitingCount { get; set; }
    public int InServiceCount { get; set; }
    public int AcceptedTodayCount { get; set; }
    public int TicketsIssuedTodayCount { get; set; }

    public int DoctorsOnShiftCount { get; set; }

    public int DoctorsTotalCount { get; set; }

    public IReadOnlyList<DashboardQueueRowDto> ActiveQueue { get; set; } = [];
    public IReadOnlyList<DoctorLoadCardDto> DoctorLoadCards { get; set; } = [];
}

public sealed class DashboardQueueRowDto
{
    public int IdAppointment { get; set; }
    public string TicketNumber { get; set; } = "";
    public int TicketPriority { get; set; }
    public int CategoryPriority { get; set; }
    public string CategoryName { get; set; } = "";
    public int WaitingMinutes { get; set; }
    public int NeededSpecialtiesCount { get; set; }
    public int CompletedSpecialtiesCount { get; set; }
    public int? IdCategory { get; set; }
    public int IdSpecialty { get; set; }
    public int IdStatusItem { get; set; }
    public string StatusLabel { get; set; } = "";
    public string StatusCode { get; set; } = "";
}

public sealed class DoctorLoadCardDto
{
    public int IdDoctor { get; set; }
    public string FullName { get; set; } = "";
    public string Specialty { get; set; } = "";
    public int IdSpecialty { get; set; }
    public string Cabinet { get; set; } = "";
    public bool IsOnShift { get; set; }
    public bool IsInService { get; set; }
    public string? CurrentTicketNumber { get; set; }
    public int? CurrentServiceMinutes { get; set; }
    public int? NormServiceMinutes { get; set; }
    public int QueueLength { get; set; }
}
