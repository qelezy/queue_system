using System.Globalization;
using WebApplication.Models;

namespace WebApplication.Services.Reports.Catalog;

internal static class ArrivedAndCompletedReportBuilder
{
    internal static readonly string[] ColumnHeaders =
    [
        "Дата",
        "Категория",
        "Зарегистрированных приёмов",
        "Неявок на приёмы",
        "Приёмов с завершённым маршрутом",
        "Приёмов с незавершённым обслуживанием"
    ];

    private static readonly int[] TotalsLabelColSpans = [2, 0, 1, 1, 1, 1];

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<ArrivedAppointmentObservation> appointments,
        IReadOnlyList<ArrivedListItemObservation> listItems,
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
            var appointmentNoShows = CatalogReportShared.CountAppointmentsWithoutListItems(
                appointmentIds,
                rel.Select(li => li.IdAppointment));

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
                appointmentNoShows,
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
                d.AppointmentNoShows.ToString(CultureInfo.InvariantCulture),
                d.FullyCompletedAppointments.ToString(CultureInfo.InvariantCulture),
                d.AppointmentsIncomplete.ToString(CultureInfo.InvariantCulture)
            ]));
        }

        var previewCharts = ReportPreviewChartDescriptors.ForArrivedCompletedAppointmentMix(
            detailData.Sum(d => d.AppointmentNoShows),
            detailData.Sum(d => d.FullyCompletedAppointments),
            detailData.Sum(d => d.AppointmentsIncomplete));

        var model = new ReportResultViewModel
        {
            ColumnHeaders = [..ColumnHeaders],
            Rows = detailRows,
            PreviewCharts = previewCharts
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
            detailData.Sum(d => d.AppointmentsCount).ToString(CultureInfo.InvariantCulture),
            detailData.Sum(d => d.AppointmentNoShows).ToString(CultureInfo.InvariantCulture),
            detailData.Sum(d => d.FullyCompletedAppointments).ToString(CultureInfo.InvariantCulture),
            detailData.Sum(d => d.AppointmentsIncomplete).ToString(CultureInfo.InvariantCulture)
        ],
            rowClass: "report-load-table__row--period-total");
    }

    internal readonly record struct ArrivedAppointmentObservation(
        int IdAppointment,
        DateOnly DateArrival,
        int IdCategory);

    internal readonly record struct ArrivedListItemObservation(
        int IdAppointment,
        TimeOnly? TimeCall,
        TimeOnly? TimeStartServicing,
        TimeOnly? TimeEndServicing);

    private readonly record struct RowAgg(
        DateOnly DateArrival,
        string CategoryName,
        int AppointmentsCount,
        int AppointmentNoShows,
        int FullyCompletedAppointments,
        int AppointmentsIncomplete);
}
