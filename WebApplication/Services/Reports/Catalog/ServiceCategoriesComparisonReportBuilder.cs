using System.Globalization;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Reports;

namespace WebApplication.Services.Reports.Catalog;

internal static class ServiceCategoriesComparisonReportBuilder
{
    internal static readonly string[] ColumnHeaders =
    [
        "Категория",
        "Обслужено пациентов",
        "Среднее ожидание до вызова",
        "Наименьшее ожидание до вызова",
        "Наибольшее ожидание до вызова",
        "Средняя длительность приёма",
        "Наименьшая длительность приёма",
        "Наибольшая длительность приёма",
        "Среднее суммарное обслуживание",
        "Наименьшее суммарное обслуживание",
        "Наибольшее суммарное обслуживание"
    ];

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<CategoryStageObservation> observations,
        ReportGenerationPurpose purpose)
    {
        var detailRows = new List<ReportResultRowViewModel>();
        var categoryLabels = new List<string>();
        var avgWaitMinutes = new List<double?>();
        var avgSvcMinutes = new List<double?>();
        var avgTotalSvcMinutes = new List<double?>();

        foreach (var g in observations.GroupBy(x => (x.IdCategory, x.CategoryName)).OrderBy(x => x.Key.CategoryName))
        {
            var subset = g.ToList();
            var ticketCount = subset.Select(x => x.IdAppointment).Distinct().Count();
            var waits = CollectValidMinutes(subset, x => x.WaitMin);
            var waitExact = CollectValidMinutes(subset, x => x.WaitMinExact);
            var svcs = CollectValidMinutes(subset, x => x.SvcMin, excludeZero: true);
            var svcExact = CollectValidMinutes(subset, x => x.SvcMinExact, excludeZero: true);

            var totalSvcPerAppointment = subset
                .GroupBy(x => x.IdAppointment)
                .Select(ag => ag.Sum(x => x.SvcMinExact is > 0 and < 10080 ? x.SvcMinExact.Value : 0))
                .Where(s => s > 0)
                .ToList();

            var avgTotalSvcExact = totalSvcPerAppointment.Count == 0 ? (double?)null : CatalogReportShared.AverageDurationMinutesExact(totalSvcPerAppointment);
            var minTotalSvcExact = totalSvcPerAppointment.Count == 0 ? (double?)null : CatalogReportShared.MinDurationMinutesExact(totalSvcPerAppointment);
            var maxTotalSvcExact = totalSvcPerAppointment.Count == 0 ? (double?)null : CatalogReportShared.MaxDurationMinutesExact(totalSvcPerAppointment);

            avgTotalSvcMinutes.Add(avgTotalSvcExact);

            var catName = string.IsNullOrWhiteSpace(g.Key.CategoryName) ? "—" : g.Key.CategoryName;

            categoryLabels.Add(catName);
            avgWaitMinutes.Add(waits.Count == 0 ? null : CatalogReportShared.AverageDurationMinutes(waits));
            avgSvcMinutes.Add(svcs.Count == 0 ? null : CatalogReportShared.AverageDurationMinutes(svcs));

            var avgWaitExact = waits.Count == 0 ? (double?)null : CatalogReportShared.AverageDurationMinutesExact(waitExact);
            var minWaitExact = waits.Count == 0 ? (double?)null : CatalogReportShared.MinDurationMinutesExact(waitExact);
            var maxWaitExact = waits.Count == 0 ? (double?)null : CatalogReportShared.MaxDurationMinutesExact(waitExact);
            var avgSvcExact = svcs.Count == 0 ? (double?)null : CatalogReportShared.AverageDurationMinutesExact(svcExact);
            var minSvcExact = svcs.Count == 0 ? (double?)null : CatalogReportShared.MinDurationMinutesExact(svcExact);
            var maxSvcExact = svcs.Count == 0 ? (double?)null : CatalogReportShared.MaxDurationMinutesExact(svcExact);

            detailRows.Add(ReportCsvCells.FromDisplayCells(
            [
                catName,
                ticketCount.ToString(CultureInfo.InvariantCulture),
                FormatStat(waits, avgWaitMinutes[^1]),
                FormatStat(waits, waits.Count == 0 ? null : CatalogReportShared.MinDurationMinutes(waits)),
                FormatStat(waits, waits.Count == 0 ? null : CatalogReportShared.MaxDurationMinutes(waits)),
                FormatStat(svcs, avgSvcMinutes[^1]),
                FormatStat(svcs, svcs.Count == 0 ? null : CatalogReportShared.MinDurationMinutes(svcs)),
                FormatStat(svcs, svcs.Count == 0 ? null : CatalogReportShared.MaxDurationMinutes(svcs)),
                FormatStat(totalSvcPerAppointment, totalSvcPerAppointment.Count == 0 ? null : CatalogReportShared.AverageDurationMinutes(totalSvcPerAppointment)),
                FormatStat(totalSvcPerAppointment, totalSvcPerAppointment.Count == 0 ? null : CatalogReportShared.MinDurationMinutes(totalSvcPerAppointment)),
                FormatStat(totalSvcPerAppointment, totalSvcPerAppointment.Count == 0 ? null : CatalogReportShared.MaxDurationMinutes(totalSvcPerAppointment))
            ],
            new Dictionary<int, double?>
            {
                [2] = avgWaitExact,
                [3] = minWaitExact,
                [4] = maxWaitExact,
                [5] = avgSvcExact,
                [6] = minSvcExact,
                [7] = maxSvcExact,
                [8] = avgTotalSvcExact,
                [9] = minTotalSvcExact,
                [10] = maxTotalSvcExact
            }));
        }

        var chartSorted = categoryLabels
            .Select((label, i) => (
                label,
                wait: avgWaitMinutes[i],
                svc: avgSvcMinutes[i],
                totalSvc: avgTotalSvcMinutes[i]))
            .OrderByDescending(x => x.totalSvc ?? double.MinValue)
            .ToList();

        var previewCharts = ReportPreviewChartDescriptors.ForServiceCategoriesComparisonHorizontalGroupedBar(
            chartSorted.Select(x => x.label).ToList(),
            chartSorted.Select(x => x.wait).ToList(),
            chartSorted.Select(x => x.svc).ToList(),
            chartSorted.Select(x => x.totalSvc).ToList());

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
        Func<CategoryStageObservation, double?> selector,
        bool excludeZero = false)
    {
        var list = new List<double>();
        foreach (var x in subset)
        {
            var v = selector(x);
            if (excludeZero ? v is > 0 and < 10080 : v is >= 0 and < 10080)
                list.Add(v.Value);
        }

        return list;
    }

    private static string FormatStat(IReadOnlyList<double> values, double? stat) =>
        values.Count == 0 || stat is null ? "—" : CatalogReportShared.FormatDuration(stat.Value);

    private static void ApplyPreviewRowCap(
        ReportResultViewModel model,
        List<ReportResultRowViewModel> detailRows,
        ReportGenerationPurpose purpose)
    {
        if (purpose != ReportGenerationPurpose.JsonPreview)
            return;

        if (detailRows.Count > ReportPreviewLimits.MaxTableRows)
        {
            model.PreviewRowsTotal = detailRows.Count;
            model.PreviewRowLimit = ReportPreviewLimits.MaxTableRows;
            model.Rows = [..detailRows.Take(ReportPreviewLimits.MaxTableRows)];
            return;
        }

        CatalogReportShared.ApplyPreviewRowCap(model, purpose);
    }

    internal static double? ComputeWaitMinutes<T>(
        DateOnly dateArrival,
        TimeOnly timeArrival,
        IReadOnlyList<T> orderedStages,
        int stageIndex,
        TimeOnly timeCall)
        where T : CatalogReportWaitingHelper.IWaitStageRow =>
        CatalogReportWaitingHelper.TryComputeWaitBeforeCallMinutes(
            dateArrival,
            timeArrival,
            orderedStages,
            stageIndex,
            timeCall);

    internal static double? ComputeSvcMinutes(DateOnly dateArrival, TimeOnly start, TimeOnly end)
    {
        return CatalogReportShared.ComputeDurationMinutes(
            EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, start),
            EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, end));
    }

    internal static double? ComputeSvcMinutesExact(DateOnly dateArrival, TimeOnly start, TimeOnly end) =>
        CatalogReportShared.ComputeDurationMinutesExact(dateArrival, start, end);

    internal static double? ComputeWaitMinutesExact<T>(
        DateOnly dateArrival,
        TimeOnly timeArrival,
        IReadOnlyList<T> orderedStages,
        int stageIndex,
        TimeOnly timeCall)
        where T : CatalogReportWaitingHelper.IWaitStageRow =>
        CatalogReportWaitingHelper.TryComputeWaitBeforeCallMinutesExact(
            dateArrival,
            timeArrival,
            orderedStages,
            stageIndex,
            timeCall);

    internal readonly record struct CategoryStageObservation(
        int IdAppointment,
        int IdCategory,
        string CategoryName,
        double? WaitMin,
        double? WaitMinExact,
        double? SvcMin,
        double? SvcMinExact);
}
