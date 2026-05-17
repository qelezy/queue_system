using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogReportAnalysisHelper
{
    internal static string FormatCabinetLabel(string? cabinetNumber) =>
        string.IsNullOrWhiteSpace(cabinetNumber)
            ? "—"
            : "Каб. " + cabinetNumber.Trim();

    internal static double? ComputeSvcMinutes(DateOnly dateArrival, TimeOnly start, TimeOnly end)
    {
        var svcMin = (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, end)
                      - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, start)).TotalMinutes;
        return svcMin >= 0 && svcMin < 10080 ? svcMin : null;
    }
}
