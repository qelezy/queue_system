using System.Globalization;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Reports.Charts;
using WebApplication.Services.Reports.Intervals;

namespace WebApplication.Services.Reports.Catalog;

internal static class RouteAndPausesReportBuilder
{
    private const double MaxMinuteContribution = 10080;

    internal static readonly string[] ColumnHeaders =
    [
        "Дата",
        "Интервал полного обслуживания",
        "Этапов",
        "Суммарное время обслуживания, мин",
        "Сумма пауз до начала приёма, мин"
    ];

    private static readonly int[] TotalsLabelColSpans = [2, 0, 1, 1, 1];

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<RouteStageObservation> stages,
        DateTime periodFrom,
        DateTime periodTo,
        ReportGenerationPurpose purpose)
    {
        var detailData = new List<RowAgg>();

        foreach (var grp in stages.GroupBy(x => x.IdAppointment))
        {
            if (grp.Count() < 2)
                continue;

            var ordered = OrderStages(grp);
            var first = ordered[0];
            if (!AppointmentQualifies(first.DateArrival, ordered, periodFrom, periodTo))
                continue;

            var routeDuration = SumRouteDurationMinutes(first.DateArrival, ordered, periodFrom, periodTo);
            var pauseSum = SumPauseMinutes(first.DateArrival, ordered, periodFrom, periodTo);
            var interval = FormatFullServiceInterval(first.DateArrival, ordered, periodFrom, periodTo);

            detailData.Add(new RowAgg(
                first.DateArrival,
                interval,
                ordered.Count,
                routeDuration,
                pauseSum));
        }

        detailData = detailData
            .OrderBy(x => x.DateArrival)
            .ThenByDescending(x => x.PauseSum)
            .ThenBy(x => x.ServiceInterval, StringComparer.OrdinalIgnoreCase)
            .ToList();

        DateOnly? prevDate = null;
        var detailRows = new List<ReportResultRowViewModel>();
        foreach (var d in detailData)
        {
            var dateCell = prevDate == d.DateArrival
                ? ""
                : d.DateArrival.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            prevDate = d.DateArrival;

            detailRows.Add(ReportResultRowViewModel.FromCells(
            [
                dateCell,
                d.ServiceInterval,
                d.StageCount.ToString(CultureInfo.InvariantCulture),
                CatalogReportShared.FormatMetric(d.RouteDuration),
                CatalogReportShared.FormatMetric(d.PauseSum)
            ]));
        }

        var model = new ReportResultViewModel
        {
            ColumnHeaders = [..ColumnHeaders],
            Rows = detailRows,
            PreviewCharts = BuildPreviewCharts(detailData)
        };

        ApplyPreviewAndTotals(model, detailData, detailRows, purpose);
        return model;
    }

    private static void ApplyPreviewAndTotals(
        ReportResultViewModel model,
        List<RowAgg> detailData,
        List<ReportResultRowViewModel> detailRows,
        ReportGenerationPurpose purpose)
    {
        CatalogReportPreviewHelper.ApplyDetailPreviewAndTotals(
            model,
            detailRows,
            detailData,
            purpose,
            (rows, data) => AppendTotalsBlock(rows, data, CatalogReportPreviewHelper.PeriodTotalsLabel),
            BuildTotalsBlockRows);
    }

    private static void AppendTotalsBlock(List<ReportResultRowViewModel> rows, List<RowAgg> detailData, string label)
    {
        foreach (var r in BuildTotalsBlockRows(detailData, label))
            rows.Add(r);
    }

    private static IEnumerable<ReportResultRowViewModel> BuildTotalsBlockRows(List<RowAgg> detailData, string label)
    {
        yield return ReportResultRowViewModel.FromCells(
            [label, "", "", "", ""],
            rowClass: "report-load-table__row--totals-start",
            cellColSpans: TotalsLabelColSpans);
        yield return ReportResultRowViewModel.FromCells(
        [
            "",
            "—",
            detailData.Sum(d => d.StageCount).ToString(CultureInfo.InvariantCulture),
            CatalogReportShared.FormatMetric(detailData.Sum(d => d.RouteDuration)),
            CatalogReportShared.FormatMetric(detailData.Sum(d => d.PauseSum))
        ],
            rowClass: "report-load-table__row--period-total");
    }

    private static List<ReportPreviewChartDescriptor>? BuildPreviewCharts(IReadOnlyList<RowAgg> detailData)
    {
        if (detailData.Count == 0)
            return null;

        var byDay = detailData.GroupBy(d => d.DateArrival).OrderBy(g => g.Key).ToList();
        var chartDays = byDay.Select(g => g.Key).ToList();
        var datasets = new List<ReportPreviewChartDataset>
        {
            new()
            {
                Label = "Обслуживание, мин",
                Values = byDay.Select(g => g.Sum(x => x.RouteDuration)).ToList()
            },
            new()
            {
                Label = "Паузы, мин",
                Values = byDay.Select(g => g.Sum(x => x.PauseSum)).ToList()
            }
        };
        var axis = GroupedBarChartTimeAxis.Prepare(chartDays, datasets, GroupedBarBucketAggregation.Sum);
        var previewCharts = ReportPreviewChartDescriptors.ForRouteAndPausesDailyGroupedBar(
            axis.Labels.ToList(),
            axis.Datasets.ToList());
        GroupedBarChartTimeAxis.SetGroupedBarFootnote(previewCharts, axis.Footnote);
        return previewCharts;
    }

    internal static List<RouteStageObservation> OrderStages(IEnumerable<RouteStageObservation> stages) =>
        stages.OrderBy(x => x.TimeStartServicing ?? TimeOnly.MaxValue).ToList();

    internal static bool AppointmentQualifies(
        DateOnly dateArrival,
        IReadOnlyList<RouteStageObservation> ordered,
        DateTime periodFrom,
        DateTime periodTo)
    {
        foreach (var stage in ordered)
        {
            var servicing = TryGetServicingInterval(dateArrival, stage);
            if (servicing.HasValue && IntersectsPeriod(servicing.Value, periodFrom, periodTo))
                return true;
        }

        foreach (var stage in ordered)
        {
            var pause = TryGetCallToStartPauseInterval(dateArrival, stage);
            if (pause.HasValue && IntersectsPeriod(pause.Value, periodFrom, periodTo))
                return true;
        }

        return false;
    }

    internal static DateTimeInterval? TryGetFullServiceInterval(
        DateOnly dateArrival,
        IReadOnlyList<RouteStageObservation> ordered)
    {
        if (ordered.Count == 0)
            return null;

        var first = ordered[0];
        var last = ordered[^1];
        var endTime = last.TimeEndServicing ?? last.TimeComplete;
        if (!endTime.HasValue)
            return null;

        var start = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, first.TimeArrival);
        var end = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, endTime.Value);
        if (start >= end)
            return null;
        return new DateTimeInterval(start, end);
    }

    internal static string FormatFullServiceInterval(
        DateOnly dateArrival,
        IReadOnlyList<RouteStageObservation> ordered,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var full = TryGetFullServiceInterval(dateArrival, ordered);
        if (!full.HasValue)
            return "—";
        var clipped = IntervalOperations.ClipToRange(full.Value, periodFrom, periodTo);
        if (!clipped.HasValue)
            return "—";
        return FormatTimeSpan(clipped.Value.Start, clipped.Value.End);
    }

    internal static double SumRouteDurationMinutes(
        DateOnly dateArrival,
        IReadOnlyList<RouteStageObservation> ordered,
        DateTime periodFrom,
        DateTime periodTo)
    {
        double sum = 0;
        foreach (var stage in ordered)
        {
            var servicing = TryGetServicingInterval(dateArrival, stage);
            if (!servicing.HasValue)
                continue;
            sum += ClippedMinutesContribution(servicing.Value, periodFrom, periodTo);
        }

        return sum;
    }

    internal static double SumPauseMinutes(
        DateOnly dateArrival,
        IReadOnlyList<RouteStageObservation> ordered,
        DateTime periodFrom,
        DateTime periodTo)
    {
        double pauseSum = 0;
        foreach (var stage in ordered)
        {
            var pause = TryGetCallToStartPauseInterval(dateArrival, stage);
            if (!pause.HasValue)
                continue;
            pauseSum += ClippedMinutesContribution(pause.Value, periodFrom, periodTo);
        }

        return pauseSum;
    }

    internal static DateTimeInterval? TryGetServicingInterval(DateOnly dateArrival, RouteStageObservation stage)
    {
        if (!stage.TimeStartServicing.HasValue || !stage.TimeEndServicing.HasValue)
            return null;
        var start = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, stage.TimeStartServicing.Value);
        var end = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, stage.TimeEndServicing.Value);
        if (start >= end)
            return null;
        return new DateTimeInterval(start, end);
    }

    internal static DateTimeInterval? TryGetCallToStartPauseInterval(
        DateOnly dateArrival,
        RouteStageObservation stage)
    {
        if (!stage.TimeCall.HasValue || !stage.TimeStartServicing.HasValue)
            return null;
        var start = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, stage.TimeCall.Value);
        var end = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, stage.TimeStartServicing.Value);
        if (start >= end)
            return null;
        return new DateTimeInterval(start, end);
    }

    internal static bool IntersectsPeriod(DateTimeInterval interval, DateTime periodFrom, DateTime periodTo) =>
        interval.End >= periodFrom && interval.Start <= periodTo;

    private static string FormatTimeSpan(DateTime start, DateTime end) =>
        $"{start.ToString("HH:mm", CultureInfo.InvariantCulture)}–{end.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    private static double ClippedMinutesContribution(
        DateTimeInterval interval,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var clipped = IntervalOperations.ClipToRange(interval, periodFrom, periodTo);
        if (!clipped.HasValue)
            return 0;
        var minutes = clipped.Value.Duration.TotalMinutes;
        if (minutes < 0 || minutes >= MaxMinuteContribution)
            return 0;
        return minutes;
    }

    private sealed record RowAgg(
        DateOnly DateArrival,
        string ServiceInterval,
        int StageCount,
        double RouteDuration,
        double PauseSum);

    internal readonly record struct RouteStageObservation(
        int IdAppointment,
        DateOnly DateArrival,
        string? Info,
        TimeOnly TimeArrival,
        TimeOnly? TimeComplete,
        TimeOnly? TimeCall,
        TimeOnly? TimeStartServicing,
        TimeOnly? TimeEndServicing);
}
