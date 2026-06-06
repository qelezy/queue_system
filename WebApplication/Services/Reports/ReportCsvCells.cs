using WebApplication.Models.Reports.ViewModels;
using WebApplication.Services.Reports.Catalog;

namespace WebApplication.Services.Reports;

internal static class ReportCsvCells
{
    internal static ReportResultRowViewModel FromDisplayCells(
        IReadOnlyList<string> displayCells,
        IReadOnlyDictionary<int, double?> durationMinutesByColumn,
        string? rowClass = null,
        IReadOnlyList<int>? cellColSpans = null)
    {
        var csv = displayCells.ToList();
        foreach (var (columnIndex, minutes) in durationMinutesByColumn)
        {
            if (columnIndex < 0 || columnIndex >= csv.Count)
                continue;

            csv[columnIndex] = CatalogReportShared.FormatDurationMinutesForCsv(minutes);
        }

        return new ReportResultRowViewModel
        {
            Cells = displayCells.ToList(),
            CsvCells = csv,
            RowClass = rowClass,
            CellColSpans = cellColSpans?.ToList()
        };
    }
}
