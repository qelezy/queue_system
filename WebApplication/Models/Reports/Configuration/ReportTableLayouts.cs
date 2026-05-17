namespace WebApplication.Models.Reports.Configuration;

public static class ReportTableLayouts
{
    public const string Standard = "standard";
    public const string DateRowspan = "dateRowspan";
}

public static class ReportPdfOrientations
{
    public const string Landscape = "landscape";
    public const string Portrait = "portrait";
}

/// <summary>Ветвление логики строк детализации в preview/export (не публичный id отчёта).</summary>
public static class ReportDetailRowKinds
{
    public const string Standard = "standard";
    public const string LoadDowntime = "loadDowntime";
    public const string RouteAndPauses = "routeAndPauses";
    public const string ArrivedCompleted = "arrivedCompleted";
    public const string AppointmentDuration = "appointmentDuration";
}
