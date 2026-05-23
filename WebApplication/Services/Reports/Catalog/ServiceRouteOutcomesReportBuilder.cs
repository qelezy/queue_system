using System.Globalization;
using WebApplication.Services.Reports.Charts;

namespace WebApplication.Services.Reports.Catalog;

internal static class ServiceRouteOutcomesReportBuilder
{
    internal static readonly string[] ColumnHeaders =
    [
        "Дата",
        "Категория обслуживания",
        "Приёмов",
        "С завершённым маршрутом",
        "С незавершённым обслуживанием"
    ];

    private static readonly int[] TotalsLabelColSpans = [2, 0, 1, 1, 1];

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<CatalogAppointmentObservations.AppointmentObservation> appointments,
        IReadOnlyList<CatalogAppointmentObservations.ListItemObservation> listItems,
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

            var appointmentsCount = appointmentIds.Count;

            var appointmentsIncomplete = appointmentIds.Count(id =>
            {
                if (!itemsByAppointment.TryGetValue(id, out var stages) || stages.Count == 0)
                    return false;
                return stages.Any(li => !li.TimeEndServicing.HasValue);
            });

            var fullyCompleted = appointmentIds.Count(id =>
            {
                if (!itemsByAppointment.TryGetValue(id, out var stages) || stages.Count == 0)
                    return false;
                return stages.All(li => li.TimeEndServicing.HasValue);
            });

            detailData.Add(new RowAgg(
                g.Key.DateArrival,
                catName,
                appointmentsCount,
                fullyCompleted,
                appointmentsIncomplete));
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
                d.FullyCompletedAppointments.ToString(CultureInfo.InvariantCulture),
                d.AppointmentsIncomplete.ToString(CultureInfo.InvariantCulture)
            ]));
        }

        var periodTotals = ComputePeriodChartTotals(detailData);
        var axisDatasets = new List<ReportPreviewChartDataset>
        {
            new() { Label = "С завершённым маршрутом", Values = periodTotals.CompletedPerDay },
            new() { Label = "С незавершённым обслуживанием", Values = periodTotals.IncompletePerDay }
        };
        var axis = GroupedBarChartTimeAxis.Prepare(
            periodTotals.ChartDays,
            axisDatasets,
            GroupedBarBucketAggregation.Sum);

        var previewCharts = ReportPreviewChartDescriptors.ForServiceRouteOutcomesCharts(
            periodTotals.TotalCompleted,
            periodTotals.TotalIncomplete,
            axis.Labels.ToList(),
            axis.Datasets[0].Values.ToList(),
            axis.Datasets[1].Values.ToList());
        GroupedBarChartTimeAxis.SetGroupedBarFootnote(previewCharts, axis.Footnote);

        var model = new ReportResultViewModel
        {
            ColumnHeaders = [..ColumnHeaders],
            Rows = detailRows,
            PreviewCharts = previewCharts
        };

        ApplyPreviewAndTotals(model, detailData, detailRows, purpose);
        return model;
    }

    private static PeriodChartTotals ComputePeriodChartTotals(List<RowAgg> detailData)
    {
        var totalCompleted = detailData.Sum(d => d.FullyCompletedAppointments);
        var totalIncomplete = detailData.Sum(d => d.AppointmentsIncomplete);

        if (detailData.Count == 0)
            return new PeriodChartTotals(0, 0, [], [], []);

        var fromDo = detailData.Min(d => d.DateArrival);
        var toDo = detailData.Max(d => d.DateArrival);

        var chartDays = new List<DateOnly>();
        var completedByDay = new Dictionary<DateOnly, double>();
        var incompleteByDay = new Dictionary<DateOnly, double>();
        for (var d = fromDo; d <= toDo; d = d.AddDays(1))
        {
            chartDays.Add(d);
            completedByDay[d] = 0;
            incompleteByDay[d] = 0;
        }

        foreach (var row in detailData)
        {
            completedByDay[row.DateArrival] += row.FullyCompletedAppointments;
            incompleteByDay[row.DateArrival] += row.AppointmentsIncomplete;
        }

        var completedSeries = chartDays.Select(d => completedByDay[d]).ToList();
        var incompleteSeries = chartDays.Select(d => incompleteByDay[d]).ToList();

        return new PeriodChartTotals(
            totalCompleted,
            totalIncomplete,
            chartDays,
            completedSeries,
            incompleteSeries);
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
            detailData.Sum(d => d.AppointmentsCount).ToString(CultureInfo.InvariantCulture),
            detailData.Sum(d => d.FullyCompletedAppointments).ToString(CultureInfo.InvariantCulture),
            detailData.Sum(d => d.AppointmentsIncomplete).ToString(CultureInfo.InvariantCulture)
        ],
            rowClass: "report-load-table__row--period-total");
    }

    private readonly record struct RowAgg(
        DateOnly DateArrival,
        string CategoryName,
        int AppointmentsCount,
        int FullyCompletedAppointments,
        int AppointmentsIncomplete);

    private readonly record struct PeriodChartTotals(
        int TotalCompleted,
        int TotalIncomplete,
        List<DateOnly> ChartDays,
        List<double> CompletedPerDay,
        List<double> IncompletePerDay);
}
