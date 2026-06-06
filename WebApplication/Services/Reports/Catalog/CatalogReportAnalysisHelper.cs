using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogReportAnalysisHelper
{
    internal static string FormatCabinetLabel(string? cabinetNumber) =>
        string.IsNullOrWhiteSpace(cabinetNumber)
            ? "—"
            : cabinetNumber.Trim();

    internal static double? ComputeSvcMinutes(DateOnly dateArrival, TimeOnly start, TimeOnly end)
    {
        return CatalogReportShared.ComputeDurationMinutes(
            EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, start),
            EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, end));
    }

    internal static double? ComputeSvcMinutesExact(DateOnly dateArrival, TimeOnly start, TimeOnly end) =>
        CatalogReportShared.ComputeDurationMinutesExact(dateArrival, start, end);
}
