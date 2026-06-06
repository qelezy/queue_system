namespace WebApplication.Services.Demo;

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
        (1, "Диспансеризация"),
        (2, "Медкомиссии пр."),
        (3, "Медкомиссии"),
        (4, "Платный приём пр."),
        (5, "Профосмотры"),
        (6, "Профосмотры пр."),
        (7, "УМО"),
        (8, "УМО пр.")
    ];
}
