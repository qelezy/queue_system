using System.Globalization;
using WebApplication.Models;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Reports.Intervals;

namespace WebApplication.Services.Reports.Catalog;

internal static class RouteAndPausesReportBuilder
{
    private const double MaxMinuteContribution = 10080;

    internal static readonly string[] ColumnHeaders =
    [
        "Дата",
        "Пациент",
        "Интервал полного обслуживания",
        "Этапов",
        "Суммарное время прохождения, мин",
        "Сумма пауз между этапами, мин"
    ];

    private static readonly int[] TotalsLabelColSpans = [3, 0, 0, 1, 1, 1];

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
            var patient = string.IsNullOrWhiteSpace(first.Info) ? "—" : first.Info.Trim();
            var interval = FormatFullServiceInterval(first.DateArrival, ordered, periodFrom, periodTo);

            detailData.Add(new RowAgg(
                first.DateArrival,
                patient,
                interval,
                ordered.Count,
                routeDuration,
                pauseSum));
        }

        detailData = detailData
            .OrderBy(x => x.DateArrival)
            .ThenByDescending(x => x.PauseSum)
            .ThenBy(x => x.Patient, StringComparer.OrdinalIgnoreCase)
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
                d.Patient,
                d.ServiceInterval,
                d.StageCount.ToString(CultureInfo.InvariantCulture),
                CatalogReportShared.F1(d.RouteDuration),
                CatalogReportShared.F1(d.PauseSum)
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
        if (purpose != ReportGenerationPurpose.JsonPreview && detailData.Count > 0)
        {
            AppendTotalsBlock(model.Rows, detailData, "Итого за период");
            return;
        }

        if (purpose == ReportGenerationPurpose.JsonPreview
            && detailRows.Count > ReportPreviewLimits.MaxTableRows)
        {
            const int previewTailReserved = 3;
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
                    ""
                ],
                rowClass: "report-load-table__row--preview-truncated-hint"),
                ..BuildTotalsBlockRows(detailData, "Итого (по полным данным)")
            ];
            return;
        }

        CatalogReportShared.ApplyPreviewRowCap(model, purpose);
        if (purpose == ReportGenerationPurpose.JsonPreview && detailData.Count > 0)
            AppendTotalsBlock(model.Rows, detailData, "Итого за период");
    }

    private static void AppendTotalsBlock(List<ReportResultRowViewModel> rows, List<RowAgg> detailData, string label)
    {
        foreach (var r in BuildTotalsBlockRows(detailData, label))
            rows.Add(r);
    }

    private static IEnumerable<ReportResultRowViewModel> BuildTotalsBlockRows(List<RowAgg> detailData, string label)
    {
        yield return ReportResultRowViewModel.FromCells(
            [label, "", "", "", "", ""],
            rowClass: "report-load-table__row--totals-start",
            cellColSpans: TotalsLabelColSpans);
        yield return ReportResultRowViewModel.FromCells(
        [
            "",
            "—",
            "—",
            detailData.Sum(d => d.StageCount).ToString(CultureInfo.InvariantCulture),
            CatalogReportShared.F1(detailData.Sum(d => d.RouteDuration)),
            CatalogReportShared.F1(detailData.Sum(d => d.PauseSum))
        ],
            rowClass: "report-load-table__row--period-total");
    }

    private static List<ReportPreviewChartDescriptor>? BuildPreviewCharts(IReadOnlyList<RowAgg> detailData)
    {
        if (detailData.Count == 0)
            return null;

        var byDay = detailData.GroupBy(d => d.DateArrival).OrderBy(g => g.Key).ToList();
        var dayLabels = byDay.Select(g => CatalogReportShared.FormatChartDayLabel(g.Key)).ToList();
        return ReportPreviewChartDescriptors.ForRouteAndPausesDailyGroupedBar(
            dayLabels,
            [
                new ReportPreviewChartDataset
                {
                    Label = "Прохождение, мин",
                    Values = byDay.Select(g => g.Sum(x => x.RouteDuration)).ToList()
                },
                new ReportPreviewChartDataset
                {
                    Label = "Паузы, мин",
                    Values = byDay.Select(g => g.Sum(x => x.PauseSum)).ToList()
                }
            ]);
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

        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var pause = TryGetPauseInterval(dateArrival, ordered, i);
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
        var startTime = first.TimeCall ?? first.TimeStartServicing ?? (TimeOnly?)first.TimeArrival;
        var endTime = last.TimeEndServicing ?? last.TimeComplete;
        if (!startTime.HasValue || !endTime.HasValue)
            return null;

        var start = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, startTime.Value);
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
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var pause = TryGetPauseInterval(dateArrival, ordered, i);
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

    internal static DateTimeInterval? TryGetPauseInterval(
        DateOnly dateArrival,
        IReadOnlyList<RouteStageObservation> ordered,
        int index)
    {
        var endCur = ordered[index].TimeEndServicing;
        var startNext = ordered[index + 1].TimeStartServicing;
        if (!endCur.HasValue || !startNext.HasValue)
            return null;
        var start = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, endCur.Value);
        var end = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, startNext.Value);
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
        string Patient,
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
