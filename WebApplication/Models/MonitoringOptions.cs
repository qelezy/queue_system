namespace WebApplication.Models;

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>Первый час рабочего дня на графиках (включитель).</summary>
    public int WorkdayStartHour { get; set; } = 8;

    /// <summary>Час после последнего слота (последний слот WorkdayEndHour - 1).</summary>
    public int WorkdayEndHour { get; set; } = 19;

    public int HeatmapTopN { get; set; } = 15;

    public int ManagerMaxRangeDays { get; set; } = 31;

    /// <summary>Кэш результата проверки подключения к ElectronicQueue (секунды).</summary>
    public int QueueAvailabilityCacheSeconds { get; set; } = 30;

    /// <summary>
    /// Минимальная длительность межэтапной паузы (мин) для строки реестра в отчёте «Необслуженные и разрывы».
    /// </summary>
    public int InterStagePauseIncidentThresholdMinutes { get; set; } = 30;
}
