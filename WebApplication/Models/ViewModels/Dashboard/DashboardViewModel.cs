namespace WebApplication.Models.ViewModels.Dashboard;

public class DashboardViewModel
{
    public int WaitingCount { get; set; }
    public int InServiceCount { get; set; }

    public int AcceptedTodayCount { get; set; }

    public int TicketsIssuedTodayCount { get; set; }

    public IReadOnlyList<DashboardQueueRowViewModel> ActiveQueue { get; set; } = [];

    public DashboardQueueFilterViewModel QueueFilters { get; set; } = new();

    public IReadOnlyList<DoctorLoadCardViewModel> DoctorLoadCards { get; set; } = [];

    public int DoctorsOnShiftCount { get; set; }

    public int DoctorsTotalCount { get; set; }

    public DashboardUiVisibility Ui { get; set; } = new();
}

public sealed class DashboardQueueTableViewModel
{
    public IReadOnlyList<DashboardQueueRowViewModel> Rows { get; set; } = [];
    public DashboardQueueFilterViewModel Filters { get; set; } = new();
}

public sealed class DashboardDoctorLoadViewModel
{
    public IReadOnlyList<DoctorLoadCardViewModel> Cards { get; set; } = [];
    public DashboardQueueFilterViewModel Filters { get; set; } = new();
    public int DoctorsOnShiftCount { get; set; }
    public int DoctorsTotalCount { get; set; }
}

public sealed class DashboardQueueFilterViewModel
{
    public IReadOnlyList<DashboardFilterOption> Specialties { get; set; } = [];
    public IReadOnlyList<DashboardFilterOption> Statuses { get; set; } = [];
}

public sealed record DashboardFilterOption(int Id, string Label);

public sealed class DashboardQueueRowViewModel
{
    public int IdAppointment { get; set; }
    public string TicketNumber { get; set; } = "";
    public int TicketPriority { get; set; }
    public int CategoryPriority { get; set; }
    public int WaitingMinutes { get; set; }
    public string CurrentCabinet { get; set; } = "";

    public string CurrentDoctor { get; set; } = "";

    public string Specialty { get; set; } = "";

    public int IdSpecialty { get; set; }
    public int IdStatusItem { get; set; }

    public string StatusLabel { get; set; } = "";
    public string StatusCode { get; set; } = "";
}

public sealed class DoctorLoadCardViewModel
{
    public int IdDoctor { get; set; }
    public string FullName { get; set; } = "";
    public string Specialty { get; set; } = "";
    public int IdSpecialty { get; set; }
    
    public string Cabinet { get; set; } = "";
    public bool IsOnShift { get; set; }
    public bool IsInService { get; set; }
    public int? CurrentServiceMinutes { get; set; }
    public int? NormServiceMinutes { get; set; }
    public int QueueLength { get; set; }
}
