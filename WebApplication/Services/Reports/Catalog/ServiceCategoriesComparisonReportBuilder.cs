using System.Globalization;
using WebApplication.Models;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class ServiceCategoriesComparisonReportBuilder
{
    internal static readonly string[] ColumnHeaders =
    [
        "Категория",
        "Приёмов",
        "Среднее ожидание до вызова, мин",
        "Минимальное ожидание до вызова, мин",
        "Максимальное ожидание до вызова, мин",
        "Средняя длительность приёма, мин",
        "Минимальная длительность приёма, мин",
        "Максимальная длительность приёма, мин"
    ];

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<CategoryStageObservation> observations,
        ReportGenerationPurpose purpose)
    {
        var detailRows = new List<ReportResultRowViewModel>();

        foreach (var g in observations.GroupBy(x => (x.IdCategory, x.CategoryName)).OrderBy(x => x.Key.CategoryName))
        {
            var subset = g.ToList();
            var ticketCount = subset.Select(x => x.IdAppointment).Distinct().Count();
            var waits = CollectValidMinutes(subset, x => x.WaitMin);
            var svcs = CollectValidMinutes(subset, x => x.SvcMin);

            var catName = string.IsNullOrWhiteSpace(g.Key.CategoryName) ? "—" : g.Key.CategoryName;

            detailRows.Add(ReportResultRowViewModel.FromCells(
            [
                catName,
                ticketCount.ToString(CultureInfo.InvariantCulture),
                FormatStat(waits, waits.Count == 0 ? null : waits.Average()),
                FormatStat(waits, waits.Count == 0 ? null : waits.Min()),
                FormatStat(waits, waits.Count == 0 ? null : waits.Max()),
                FormatStat(svcs, svcs.Count == 0 ? null : svcs.Average()),
                FormatStat(svcs, svcs.Count == 0 ? null : svcs.Min()),
                FormatStat(svcs, svcs.Count == 0 ? null : svcs.Max())
            ]));
        }

        var (totalSingle, totalMulti) = AggregateStageMixCounts(observations);
        var previewCharts = ReportPreviewChartDescriptors.ForMultiStageRoutesMix(totalSingle, totalMulti);

        var model = new ReportResultViewModel
        {
            ColumnHeaders = [..ColumnHeaders],
            Rows = detailRows,
            PreviewCharts = previewCharts
        };

        ApplyPreviewRowCap(model, detailRows, purpose);
        return model;
    }

    private static List<double> CollectValidMinutes(
        IReadOnlyList<CategoryStageObservation> subset,
        Func<CategoryStageObservation, double?> selector)
    {
        var list = new List<double>();
        foreach (var x in subset)
        {
            var v = selector(x);
            if (v is >= 0 and < 10080)
                list.Add(v.Value);
        }

        return list;
    }

    private static string FormatStat(IReadOnlyList<double> values, double? stat) =>
        values.Count == 0 || stat is null ? "—" : CatalogReportShared.F1(stat.Value);

    private static (int Single, int Multi) AggregateStageMixCounts(
        IReadOnlyList<CategoryStageObservation> observations)
    {
        var single = 0;
        var multi = 0;
        foreach (var g in observations.GroupBy(x => (x.IdCategory, x.IdAppointment)))
        {
            var stageCount = g.Count();
            if (stageCount == 1)
                single++;
            else if (stageCount >= 2)
                multi++;
        }

        return (single, multi);
    }

    private static void ApplyPreviewRowCap(
        ReportResultViewModel model,
        List<ReportResultRowViewModel> detailRows,
        ReportGenerationPurpose purpose)
    {
        if (purpose != ReportGenerationPurpose.JsonPreview)
            return;

        if (detailRows.Count > ReportPreviewLimits.MaxTableRows)
        {
            const int previewTailReserved = 1;
            var maxDetail = Math.Max(0, ReportPreviewLimits.MaxTableRows - previewTailReserved);
            model.PreviewRowsTotal = detailRows.Count;
            model.PreviewRowLimit = ReportPreviewLimits.MaxTableRows;
            model.Rows =
            [
                ..detailRows.Take(maxDetail),
                ReportResultRowViewModel.FromCells(
                [
                    "…",
                    "Показаны не все строки; полный отчёт — при сохранении в файл.",
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                ],
                rowClass: "report-load-table__row--preview-truncated-hint")
            ];
            return;
        }

        CatalogReportShared.ApplyPreviewRowCap(model, purpose);
    }

    internal static double? ComputeWaitMinutes(DateOnly dateArrival, TimeOnly timeArrival, TimeOnly timeCall)
    {
        var w = (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeCall)
                 - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeArrival)).TotalMinutes;
        return w >= 0 && w < 10080 ? w : null;
    }

    internal static double? ComputeSvcMinutes(DateOnly dateArrival, TimeOnly start, TimeOnly end)
    {
        var s = (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, end)
                 - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, start)).TotalMinutes;
        return s >= 0 && s < 10080 ? s : null;
    }

    internal readonly record struct CategoryStageObservation(
        int IdAppointment,
        int IdCategory,
        string CategoryName,
        double? WaitMin,
        double? SvcMin);
}
