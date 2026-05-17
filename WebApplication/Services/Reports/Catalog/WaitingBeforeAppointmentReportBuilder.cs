using System.Globalization;
using WebApplication.Models;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

/// <summary>Сборка таблицы и диаграммы отчёта «Ожидание до приёма» (дата × интервалы в границах периода).</summary>
internal static class WaitingBeforeAppointmentReportBuilder
{
    internal static readonly string[] ColumnHeaders =
    [
        "Дата",
        "Интервал",
        "Завершённых ожиданий",
        "Среднее ожидание, мин",
        "Минимум, мин",
        "Максимум, мин"
    ];

    internal const string DayTotalsHeadingLabel = "Итого за день";
    internal const string PeriodTotalsLabel = "Итого за период";
    internal const string PeriodTotalsPreviewFullDataLabel = "Итого (по полным данным)";

    private static readonly int[] DayTotalsLabelColSpans = [2, 0, 1, 1, 1, 1];
    private static readonly int[] PeriodTotalsLabelColSpans = [2, 0, 1, 1, 1, 1];

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<WaitingObservation> observations,
        DateOnly fromDo,
        DateOnly toDo,
        DateTime periodFrom,
        DateTime periodTo,
        ReportGenerationPurpose purpose)
    {
        var model = Build(observations, fromDo, toDo, periodFrom, periodTo);
        ApplyPeriodTotalsAndPreview(model, observations, purpose);
        return model;
    }

    internal static ReportResultViewModel Build(
        IReadOnlyList<WaitingObservation> observations,
        DateOnly fromDo,
        DateOnly toDo,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var rows = new List<ReportResultRowViewModel>();
        var dayLabels = new List<string>();
        var hoursInPeriod = new SortedSet<int>();

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            foreach (var slot in GetHourSlotsForDay(day, periodFrom, periodTo))
                hoursInPeriod.Add(slot.Hour);
        }

        var hourSeries = hoursInPeriod
            .Select(h => new ReportPreviewChartDataset { Label = FormatHourChartLabel(h), Values = [] })
            .ToList();
        var hourIndex = hoursInPeriod.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i);

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            dayLabels.Add(CatalogReportShared.FormatChartDayLabel(day));
            var dayObs = observations.Where(o => o.Date == day).ToList();
            var slots = GetHourSlotsForDay(day, periodFrom, periodTo);
            var isFirstHourRow = true;

            var slotHours = slots.Select(s => s.Hour).ToHashSet();

            foreach (var slot in slots)
            {
                var hourObs = dayObs.Where(o => o.Hour == slot.Hour).Select(o => o.WaitMin).ToList();
                var metrics = FormatMetrics(hourObs);
                rows.Add(ReportResultRowViewModel.FromCells(
                [
                    isFirstHourRow ? dayLabels[^1] : "",
                    slot.IntervalLabel,
                    metrics.Count,
                    metrics.Average,
                    metrics.Min,
                    metrics.Max
                ]));
                isFirstHourRow = false;
            }

            foreach (var h in hoursInPeriod)
            {
                if (!hourIndex.TryGetValue(h, out var seriesIdx))
                    continue;

                if (!slotHours.Contains(h))
                {
                    hourSeries[seriesIdx].Values.Add(0);
                    continue;
                }

                var hourObs = dayObs.Where(o => o.Hour == h).Select(o => o.WaitMin).ToList();
                hourSeries[seriesIdx].Values.Add(hourObs.Count == 0 ? 0 : Math.Round(hourObs.Average(), 1));
            }

            var dayMetrics = FormatMetrics(dayObs.Select(o => o.WaitMin).ToList());
            rows.Add(ReportResultRowViewModel.FromCells(
                [DayTotalsHeadingLabel, "", "", "", "", ""],
                rowClass: "report-load-table__row--day-totals-heading",
                cellColSpans: DayTotalsLabelColSpans));
            rows.Add(ReportResultRowViewModel.FromCells(
                ["", "—", dayMetrics.Count, dayMetrics.Average, dayMetrics.Min, dayMetrics.Max],
                rowClass: "report-load-table__row--day-totals-end"));
        }

        return new ReportResultViewModel
        {
            ColumnHeaders = [..ColumnHeaders],
            Rows = rows,
            PreviewCharts = ReportPreviewChartDescriptors.ForWaitingBeforeAppointmentDailyGroupedBar(dayLabels, hourSeries)
        };
    }

    internal readonly record struct HourSlot(int Hour, string IntervalLabel);

    internal static List<HourSlot> GetHourSlotsForDay(DateOnly day, DateTime periodFrom, DateTime periodTo)
    {
        var periodFromDo = DateOnly.FromDateTime(periodFrom);
        var periodToDo = DateOnly.FromDateTime(periodTo);
        var dayMidnight = day.ToDateTime(TimeOnly.MinValue);
        var nextMidnight = day.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var effStart = day == periodFromDo ? periodFrom : dayMidnight;
        var effEndInclusive = day == periodToDo ? periodTo : nextMidnight.AddTicks(-1);

        if (effStart > effEndInclusive)
            return [];

        var slots = new List<HourSlot>();
        var minHour = effStart.Hour;
        var maxHour = effEndInclusive.Hour;

        for (var h = minHour; h <= maxHour; h++)
        {
            var slotStart = day.ToDateTime(new TimeOnly(h, 0));
            var slotEndExclusive = h < 23
                ? day.ToDateTime(new TimeOnly(h + 1, 0))
                : nextMidnight;

            var displayStart = slotStart > effStart ? slotStart : effStart;
            var slotEndInclusive = slotEndExclusive.AddTicks(-1);
            var displayEndInclusive = slotEndInclusive < effEndInclusive ? slotEndInclusive : effEndInclusive;

            if (displayStart > displayEndInclusive)
                continue;

            var labelEnd = slotEndExclusive <= effEndInclusive.AddTicks(1) && slotEndExclusive > displayStart
                ? slotEndExclusive
                : effEndInclusive;

            slots.Add(new HourSlot(h, FormatClippedInterval(displayStart, labelEnd)));
        }

        return slots;
    }

    internal static void ApplyPeriodTotalsAndPreview(
        ReportResultViewModel model,
        IReadOnlyList<WaitingObservation> observations,
        ReportGenerationPurpose purpose)
    {
        var detailRows = model.Rows;
        if (purpose != ReportGenerationPurpose.JsonPreview)
        {
            AppendPeriodTotals(detailRows, observations, PeriodTotalsLabel);
            return;
        }

        if (detailRows.Count > ReportPreviewLimits.MaxTableRows)
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
                ..BuildPeriodTotalsRows(observations, PeriodTotalsPreviewFullDataLabel)
            ];
            return;
        }

        AppendPeriodTotals(detailRows, observations, PeriodTotalsLabel);
    }

    internal static void AppendPeriodTotals(
        List<ReportResultRowViewModel> rows,
        IReadOnlyList<WaitingObservation> observations,
        string label)
    {
        foreach (var r in BuildPeriodTotalsRows(observations, label))
            rows.Add(r);
    }

    internal static IEnumerable<ReportResultRowViewModel> BuildPeriodTotalsRows(
        IReadOnlyList<WaitingObservation> observations,
        string label)
    {
        var metrics = FormatMetrics(observations.Select(o => o.WaitMin).ToList());
        yield return ReportResultRowViewModel.FromCells(
            [label, "", "", "", "", ""],
            rowClass: "report-load-table__row--totals-start",
            cellColSpans: PeriodTotalsLabelColSpans);
        yield return ReportResultRowViewModel.FromCells(
            ["", "—", metrics.Count, metrics.Average, metrics.Min, metrics.Max],
            rowClass: "report-load-table__row--period-total");
    }

    internal static string FormatHourInterval(int hour)
    {
        var start = hour.ToString("00", CultureInfo.InvariantCulture) + ":00";
        var end = hour < 23
            ? (hour + 1).ToString("00", CultureInfo.InvariantCulture) + ":00"
            : "00:00";
        return start + "–" + end;
    }

    internal static string FormatClippedInterval(DateTime displayStart, DateTime labelEnd)
    {
        if (labelEnd.Date > displayStart.Date
            || labelEnd == displayStart.Date.AddDays(1))
            return FormatTime(displayStart) + "–00:00";

        return FormatTime(displayStart) + "–" + FormatTime(labelEnd);
    }

    internal static string FormatTime(DateTime dt) =>
        dt.ToString("HH:mm", CultureInfo.InvariantCulture);

    internal static string FormatHourChartLabel(int hour) =>
        hour.ToString("00", CultureInfo.InvariantCulture) + ":00";

    internal static (string Count, string Average, string Min, string Max) FormatMetrics(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return ("0", "—", "—", "—");

        return (
            values.Count.ToString(CultureInfo.InvariantCulture),
            CatalogReportShared.F1(values.Average()),
            CatalogReportShared.F1(values.Min()),
            CatalogReportShared.F1(values.Max()));
    }

    internal static bool IsCallInPeriod(DateOnly dateArrival, TimeOnly timeCall, DateTime periodFrom, DateTime periodTo)
    {
        var callDt = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeCall);
        return callDt >= periodFrom && callDt <= periodTo;
    }

    internal readonly record struct WaitingObservation(DateOnly Date, int Hour, double WaitMin);
}
