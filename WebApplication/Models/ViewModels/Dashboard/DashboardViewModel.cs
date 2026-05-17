namespace WebApplication.Models.ViewModels.Dashboard;

public class DashboardViewModel
{
    public int WaitingCount { get; set; }
    public int InServiceCount { get; set; }

    /// <summary>Завершённые приёмы за сегодня (по дате прибытия в очереди).</summary>
    public int AcceptedTodayCount { get; set; }

    /// <summary>Записи за сегодня с отмеченной неявкой.</summary>
    public int NoShowTodayCount { get; set; }

    public int AvgWaitMinutes { get; set; }
    public int MaxWaitMinutes { get; set; }
    public int AvgServiceMinutes { get; set; }
    public int MaxServiceMinutes { get; set; }

    public IReadOnlyList<DashboardQueueRowViewModel> ActiveQueue { get; set; } = [];

    public IReadOnlyList<DoctorLoadCardViewModel> DoctorLoadCards { get; set; } = [];

    public DashboardUiVisibility Ui { get; set; } = new();
}

public sealed class DashboardQueueRowViewModel
{
    public int IdAppointment { get; set; }
    public string Patient { get; set; } = "";
    public int TicketPriority { get; set; }
    public int CategoryPriority { get; set; }
    public int WaitingMinutes { get; set; }
    public string CurrentCabinet { get; set; } = "";

    /// <summary>ФИО врача текущего этапа.</summary>
    public string CurrentDoctor { get; set; } = "";

    /// <summary>Специальность текущего этапа (definition).</summary>
    public string Specialty { get; set; } = "";

    /// <summary>Время записи (прибытия), только «HH:mm».</summary>
    public string ArrivalTime { get; set; } = "";

    public string StatusLabel { get; set; } = "";
    public string StatusCode { get; set; } = "";
}

public sealed class DoctorLoadCardViewModel
{
    public int IdDoctor { get; set; }
    public string FullName { get; set; } = "";
    public string Specialty { get; set; } = "";
    /// <summary>Кабинет текущего приёма («Каб. N»); пусто если врач свободен.</summary>
    public string Cabinet { get; set; } = "";
    public bool IsInService { get; set; }
    public int? CurrentServiceMinutes { get; set; }
    public int? NormServiceMinutes { get; set; }
    public int QueueLength { get; set; }
}
