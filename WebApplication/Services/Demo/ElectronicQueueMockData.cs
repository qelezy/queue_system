namespace WebApplication.Services.Demo;

/// <summary>Согласованные демо-идентификаторы и подписи для mock ElectronicQueue.</summary>
public static class ElectronicQueueMockData
{
    public static IReadOnlyList<(int Id, string Label)> Cabinets { get; } =
    [
        (1, "101"),
        (2, "102"),
        (3, "103")
    ];

    public static IReadOnlyList<(int Id, string Name)> Doctors { get; } =
    [
        (1, "Иванов А. А."),
        (2, "Петрова М. С."),
        (3, "Сидоров В. К."),
        (4, "Козлова Е. Д.")
    ];

    public static IReadOnlyList<(int Id, string Name)> Categories { get; } =
    [
        (1, "ОМС"),
        (2, "Платно")
    ];

    public static IReadOnlyList<string> BuildHourLabels(MonitoringOptions opt)
    {
        var list = new List<string>();
        for (var h = opt.WorkdayStartHour; h < opt.WorkdayEndHour; h++)
            list.Add($"{h}:00");
        return list;
    }

    public static int HourSlotCount(MonitoringOptions opt) =>
        Math.Max(0, opt.WorkdayEndHour - opt.WorkdayStartHour);
}
