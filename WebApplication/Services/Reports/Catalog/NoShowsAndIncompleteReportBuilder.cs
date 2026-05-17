using System.Globalization;

namespace WebApplication.Services.Reports.Catalog;

internal static class NoShowsAndIncompleteReportBuilder
{
    private static readonly int[] TotalsLabelColSpans = [2, 0, 1, 1, 1, 1];

    internal static string[] BuildColumnHeaders() =>
    [
        "Дата",
        "Категория обслуживания",
        "Зарегистрированных приёмов",
        "Неявок на приёмы",
        "Приёмов с незавершённым обслуживанием",
        "Доля приёмов с неявкой или незавершённым обслуживанием, %"
    ];

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<ArrivedAndCompletedReportBuilder.ArrivedAppointmentObservation> appointments,
        IReadOnlyList<ArrivedAndCompletedReportBuilder.ArrivedListItemObservation> listItems,
        IReadOnlyDictionary<int, (string Name, int Priority)> categories,
        ReportGenerationPurpose purpose)
    {
        var itemsByAppointment = listItems.GroupBy(li => li.IdAppointment).ToDictionary(g => g.Key, g => g.ToList());
        var detailData = new List<RowAgg>();

        foreach (var g in appointments
                     .GroupBy(a => (a.DateArrival, a.IdCategory))
                     .OrderBy(x => x.Key.DateArrival)
                     .ThenBy(x => categories.TryGetValue(x.Key.IdCategory, out var c) ? c.Priority : int.MaxValue)
                     .ThenBy(x =>
                         categories.TryGetValue(x.Key.IdCategory, out var c) ? (c.Name ?? "") : "",
                         StringComparer.OrdinalIgnoreCase))
        {
            var catName = categories.TryGetValue(g.Key.IdCategory, out var cat) && !string.IsNullOrEmpty(cat.Name)
                ? cat.Name
                : "—";

            var appointmentIds = g.Select(x => x.IdAppointment).ToHashSet();
            var rel = listItems.Where(li => appointmentIds.Contains(li.IdAppointment)).ToList();

            var appointmentsCount = appointmentIds.Count;
            var noShows = CatalogReportShared.CountAppointmentsWithoutListItems(
                appointmentIds,
                rel.Select(li => li.IdAppointment));

            var incomplete = 0;
            var withProblem = 0;

            foreach (var id in appointmentIds)
            {
                var isNoShow = !itemsByAppointment.TryGetValue(id, out var stages) || stages.Count == 0;
                var isIncomplete = !isNoShow
                    && stages is not null
                    && CatalogReportShared.AppointmentHasIncompleteRoute(
                        stages.Select(s => s.TimeEndServicing).ToList());

                if (isNoShow || isIncomplete)
                    withProblem++;
                if (isIncomplete)
                    incomplete++;
            }

            detailData.Add(new RowAgg(
                g.Key.DateArrival,
                catName,
                appointmentsCount,
                noShows,
                incomplete,
                withProblem));
        }

        DateOnly? prevDetailDate = null;
        var detailRows = new List<ReportResultRowViewModel>();
        foreach (var d in detailData)
        {
            var dateCell = prevDetailDate == d.DateArrival
                ? ""
                : d.DateArrival.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            prevDetailDate = d.DateArrival;

            detailRows.Add(ReportResultRowViewModel.FromCells(
            [
                dateCell,
                d.CategoryName,
                d.AppointmentsCount.ToString(CultureInfo.InvariantCulture),
                d.NoShows.ToString(CultureInfo.InvariantCulture),
                d.Incomplete.ToString(CultureInfo.InvariantCulture),
                CatalogReportShared.FormatProblemSharePercent(d.AppointmentsCount, d.WithProblem)
            ]));
        }

        var periodTotals = ComputePeriodChartTotals(detailData);

        var previewCharts = ReportPreviewChartDescriptors.ForNoShowsAndIncompleteCharts(
            periodTotals.TotalNoShows,
            periodTotals.TotalIncomplete,
            periodTotals.DayLabels,
            periodTotals.NoShowPerDay,
            periodTotals.IncompletePerDay);

        var model = new ReportResultViewModel
        {
            ColumnHeaders = [..BuildColumnHeaders()],
            Rows = detailRows,
            PreviewCharts = previewCharts
        };

        ApplyPreviewAndTotals(model, detailData, detailRows, purpose);
        return model;
    }

    private static PeriodChartTotals ComputePeriodChartTotals(List<RowAgg> detailData)
    {
        var totalNoShows = detailData.Sum(d => d.NoShows);
        var totalIncomplete = detailData.Sum(d => d.Incomplete);

        if (detailData.Count == 0)
            return new PeriodChartTotals(0, 0, [], [], []);

        var fromDo = detailData.Min(d => d.DateArrival);
        var toDo = detailData.Max(d => d.DateArrival);

        var noShowByDay = new Dictionary<DateOnly, double>();
        var incompleteByDay = new Dictionary<DateOnly, double>();
        for (var d = fromDo; d <= toDo; d = d.AddDays(1))
        {
            noShowByDay[d] = 0;
            incompleteByDay[d] = 0;
        }

        foreach (var row in detailData)
        {
            noShowByDay[row.DateArrival] += row.NoShows;
            incompleteByDay[row.DateArrival] += row.Incomplete;
        }

        var dayLabels = new List<string>();
        var noShowSeries = new List<double>();
        var incompleteSeries = new List<double>();
        for (var d = fromDo; d <= toDo; d = d.AddDays(1))
        {
            dayLabels.Add(CatalogReportShared.FormatChartDayLabel(d));
            noShowSeries.Add(noShowByDay[d]);
            incompleteSeries.Add(incompleteByDay[d]);
        }

        return new PeriodChartTotals(
            totalNoShows,
            totalIncomplete,
            dayLabels,
            noShowSeries,
            incompleteSeries);
    }

    private static void ApplyPreviewAndTotals(
        ReportResultViewModel model,
        List<RowAgg> detailData,
        List<ReportResultRowViewModel> detailRows,
        ReportGenerationPurpose purpose)
    {
        if (purpose != ReportGenerationPurpose.JsonPreview && detailData.Count > 0)
        {
            AppendTotalsBlock(model.Rows, detailData);
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
            AppendTotalsBlock(model.Rows, detailData);
    }

    private static void AppendTotalsBlock(List<ReportResultRowViewModel> rows, List<RowAgg> detailData)
    {
        foreach (var r in BuildTotalsBlockRows(detailData, "Итого за период"))
            rows.Add(r);
    }

    private static IEnumerable<ReportResultRowViewModel> BuildTotalsBlockRows(List<RowAgg> detailData, string label)
    {
        var totalAppointments = detailData.Sum(d => d.AppointmentsCount);
        var totalWithProblem = detailData.Sum(d => d.WithProblem);

        yield return ReportResultRowViewModel.FromCells(
            [label, "", "", "", "", ""],
            rowClass: "report-load-table__row--totals-start",
            cellColSpans: TotalsLabelColSpans);

        yield return ReportResultRowViewModel.FromCells(
        [
            "",
            "—",
            totalAppointments.ToString(CultureInfo.InvariantCulture),
            detailData.Sum(d => d.NoShows).ToString(CultureInfo.InvariantCulture),
            detailData.Sum(d => d.Incomplete).ToString(CultureInfo.InvariantCulture),
            CatalogReportShared.FormatProblemSharePercent(totalAppointments, totalWithProblem)
        ],
            rowClass: "report-load-table__row--period-total");
    }

    private readonly record struct RowAgg(
        DateOnly DateArrival,
        string CategoryName,
        int AppointmentsCount,
        int NoShows,
        int Incomplete,
        int WithProblem);

    private readonly record struct PeriodChartTotals(
        int TotalNoShows,
        int TotalIncomplete,
        List<string> DayLabels,
        List<double> NoShowPerDay,
        List<double> IncompletePerDay);
}
