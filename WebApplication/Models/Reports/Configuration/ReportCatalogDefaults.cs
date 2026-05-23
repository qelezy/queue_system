using WebApplication.Models.Reports.Constants;

namespace WebApplication.Models.Reports.Configuration;

/// <summary>Дефолты технических полей каталога по <see cref="ReportIds"/> и <see cref="ReportGeneratorKind"/>.</summary>
public static class ReportCatalogDefaults
{
    private static readonly IReadOnlyDictionary<string, ReportGeneratorKind> KindByReportId =
        new Dictionary<string, ReportGeneratorKind>(StringComparer.OrdinalIgnoreCase)
        {
            [ReportIds.LoadAndDowntime] = ReportGeneratorKind.LoadAndDowntime,
            [ReportIds.WaitingBeforeAppointment] = ReportGeneratorKind.WaitingBeforeAppointment,
            [ReportIds.AppointmentDuration] = ReportGeneratorKind.AppointmentDuration,
            [ReportIds.RouteAndPauses] = ReportGeneratorKind.RouteAndPauses,
            [ReportIds.ServiceRouteOutcomes] = ReportGeneratorKind.ServiceRouteOutcomes,
            [ReportIds.ServiceCategoriesComparison] = ReportGeneratorKind.ServiceCategoriesComparison,
            [ReportIds.ServiceDelays] = ReportGeneratorKind.ServiceDelays
        };

    public static bool TryResolveGeneratorKind(string reportId, out ReportGeneratorKind kind)
    {
        kind = default;
        if (string.IsNullOrWhiteSpace(reportId))
            return false;

        return KindByReportId.TryGetValue(reportId.Trim(), out kind);
    }

    public static ReportGeneratorKind ParseRequiredKind(string reportId)
    {
        if (TryResolveGeneratorKind(reportId, out var kind))
            return kind;

        throw new InvalidOperationException(
            $"Неизвестный Id отчёта «{reportId}» в Reports:Catalog. Задайте GeneratorKind явно или добавьте Id в {nameof(ReportCatalogDefaults)}.");
    }

    public static ReportCatalogPresentationDefaults GetPresentationDefaults(ReportGeneratorKind kind) =>
        kind switch
        {
            ReportGeneratorKind.LoadAndDowntime => new ReportCatalogPresentationDefaults(
                ReportTableLayouts.DateRowspan,
                ReportPdfOrientations.Landscape,
                ReportDetailRowKinds.LoadDowntime),
            ReportGeneratorKind.WaitingBeforeAppointment => new ReportCatalogPresentationDefaults(
                ReportTableLayouts.DateRowspan,
                ReportPdfOrientations.Portrait,
                ReportDetailRowKinds.WaitingBeforeAppointment),
            ReportGeneratorKind.AppointmentDuration => new ReportCatalogPresentationDefaults(
                ReportTableLayouts.DateRowspan,
                ReportPdfOrientations.Portrait,
                ReportDetailRowKinds.AppointmentDuration),
            ReportGeneratorKind.RouteAndPauses => new ReportCatalogPresentationDefaults(
                ReportTableLayouts.DateRowspan,
                ReportPdfOrientations.Landscape,
                ReportDetailRowKinds.RouteAndPauses),
            ReportGeneratorKind.ServiceRouteOutcomes => new ReportCatalogPresentationDefaults(
                ReportTableLayouts.DateRowspan,
                ReportPdfOrientations.Landscape,
                ReportDetailRowKinds.ArrivedCompleted),
            ReportGeneratorKind.ServiceCategoriesComparison => new ReportCatalogPresentationDefaults(
                ReportTableLayouts.Standard,
                ReportPdfOrientations.Landscape,
                ReportDetailRowKinds.Standard),
            ReportGeneratorKind.ServiceDelays => new ReportCatalogPresentationDefaults(
                ReportTableLayouts.Standard,
                ReportPdfOrientations.Landscape,
                ReportDetailRowKinds.Standard),
            _ => new ReportCatalogPresentationDefaults(
                ReportTableLayouts.Standard,
                ReportPdfOrientations.Landscape,
                ReportDetailRowKinds.Standard)
        };
}

public readonly record struct ReportCatalogPresentationDefaults(
    string TableLayout,
    string PdfOrientation,
    string DetailRowKind);
