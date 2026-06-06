using System.Globalization;

using WebApplication.Services.Reports;

namespace WebApplication.Services.Reports.Catalog;

internal static class ServiceDelaysReportBuilder
{
    internal const int TopN = 15;

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<ServiceDelaysQueries.EntityMetrics> metrics,
        ReportGenerationPurpose purpose)
    {
        var ranked = metrics
            .OrderByDescending(m => m.TotalDelayMin)
            .ThenBy(m => m.EntityName, StringComparer.OrdinalIgnoreCase)
            .Take(TopN)
            .ToList();

        var table = ranked
            .Select(m => ReportCsvCells.FromDisplayCells(
            [
                m.EntityName,
                m.SpecialtyLabels,
                FormatDelayMinutes(m.TotalDelayMin),
                FormatAvgDelay(m.AvgDelayMin),
                FormatDelayMinutes(m.MinDelayMin),
                FormatDelayMinutes(m.MaxDelayMin),
                m.OverNormCount.ToString(CultureInfo.InvariantCulture)
            ],
            new Dictionary<int, double?>
            {
                [2] = m.TotalDelayMinExact,
                [3] = m.AvgDelayMinExact,
                [4] = m.MinDelayMinExact,
                [5] = m.MaxDelayMinExact
            }))
            .ToList();

        var model = new ReportResultViewModel
        {
            ColumnHeaders =
            [
                "Врач",
                "Специализация",
                "Сумма задержек",
                "Средняя задержка",
                "Наименьшая задержка",
                "Наибольшая задержка",
                "Превышений норматива"
            ],
            Rows = table
        };

        CatalogReportShared.ApplyPreviewRowCap(model, purpose);
        return model;
    }

    private static string FormatDelayMinutes(double minutes) =>
        CatalogReportShared.FormatDuration(minutes);

    private static string FormatAvgDelay(double? minutes) =>
        minutes is null ? "—" : CatalogReportShared.FormatDuration(minutes.Value);
}
