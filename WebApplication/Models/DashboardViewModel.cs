namespace WebApplication.Models;

public class DashboardViewModel
{
    public int WaitingCount { get; set; }
    public int InServiceCount { get; set; }

    public int AvgWaitMinutes { get; set; }
    public int MaxWaitMinutes { get; set; }
    public int AvgServiceMinutes { get; set; }
    public int MaxServiceMinutes { get; set; }

    /// <summary>Метки часов рабочего дня, например «8:00».</summary>
    public IReadOnlyList<string> HourlyLabels { get; set; } = [];

    public IReadOnlyList<int> HourlyWaitMinutes { get; set; } = [];
    public IReadOnlyList<int> HourlyServiceMinutes { get; set; } = [];

    public IReadOnlyList<string> CabinetLoadLabels { get; set; } = [];
    public IReadOnlyList<int> CabinetCompletedToday { get; set; } = [];
    public IReadOnlyList<int> CabinetBusyPercent { get; set; } = [];

    public IReadOnlyList<string> DoctorLoadLabels { get; set; } = [];
    public IReadOnlyList<int> DoctorCompletedToday { get; set; } = [];
    public IReadOnlyList<int> DoctorBusyPercent { get; set; } = [];

    public IReadOnlyList<DashboardQueueRowViewModel> ActiveQueue { get; set; } = [];

    public ManagerAnalyticsViewModel? Manager { get; set; }

    /// <summary>Данные с дашборда взяты из демо (БД очереди недоступна).</summary>
    public bool UsingElectronicQueueMockData { get; set; }
}

public sealed class DashboardQueueRowViewModel
{
    public int IdAppointment { get; set; }
    public string Patient { get; set; } = "";
    public string PriorityDisplay { get; set; } = "";
    public int TicketPriority { get; set; }
    public int CategoryPriority { get; set; }
    public int WaitingMinutes { get; set; }
    public string CurrentCabinet { get; set; } = "";
    public string CurrentDoctor { get; set; } = "";
}

public sealed class ManagerAnalyticsViewModel
{
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public int? FilterCabinetId { get; set; }
    public int? FilterDoctorId { get; set; }
    public int? FilterCategoryId { get; set; }

    public IReadOnlyList<SelectOptionViewModel> CabinetOptions { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> DoctorOptions { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> CategoryOptions { get; set; } = [];

    /// <summary>Ось X — часы (8:00 …).</summary>
    public IReadOnlyList<string> QueueByHourLabels { get; set; } = [];

    /// <summary>Серии: одна на календарный день в периоде.</summary>
    public IReadOnlyList<ManagerDaySeriesViewModel> QueueByHourPerDay { get; set; } = [];

    /// <summary>Среднее по дням для каждого часа (длина = QueueByHourLabels).</summary>
    public IReadOnlyList<double> QueueByHourDailyAverage { get; set; } = [];

    public IReadOnlyList<HistogramBucketViewModel> WaitHistogram { get; set; } = [];

    public IReadOnlyList<NamedValueViewModel> AvgWaitByDoctor { get; set; } = [];
    public IReadOnlyList<NamedValueViewModel> AvgServiceByDoctor { get; set; } = [];

    public IReadOnlyList<string> HeatmapHourLabels { get; set; } = [];
    public IReadOnlyList<string> HeatmapRowLabels { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<double>> HeatmapValues { get; set; } = [];
    public bool HeatmapIsByDoctor { get; set; }
}

public sealed class ManagerDaySeriesViewModel
{
    public string DayLabel { get; set; } = "";
    public IReadOnlyList<int> Values { get; set; } = [];
}

public sealed class HistogramBucketViewModel
{
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public sealed class NamedValueViewModel
{
    public string Name { get; set; } = "";
    public double ValueMinutes { get; set; }
}

public sealed class SelectOptionViewModel
{
    public int? Value { get; set; }
    public string Text { get; set; } = "";
}
