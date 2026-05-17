using System.Globalization;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogReportShared
{
    internal static (DateTime PeriodFrom, DateTime PeriodTo, DateOnly FromDo, DateOnly ToDo) ParsePeriod(
        ReportGenerateRequest request)
    {
        if (!DateTime.TryParse(
                request.DateFrom,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodFrom)
            || !DateTime.TryParse(
                request.DateTo,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodTo))
        {
            periodFrom = DateTime.UtcNow.Date.AddDays(-7);
            periodTo = DateTime.UtcNow;
        }

        if (periodFrom > periodTo)
            (periodFrom, periodTo) = (periodTo, periodFrom);

        var fromDo = DateOnly.FromDateTime(periodFrom);
        var toDo = DateOnly.FromDateTime(periodTo);
        return (periodFrom, periodTo, fromDo, toDo);
    }

    internal static void ApplyPreviewRowCap(ReportResultViewModel model, ReportGenerationPurpose purpose)
    {
        if (purpose != ReportGenerationPurpose.JsonPreview)
            return;
        if (model.Rows.Count <= ReportPreviewLimits.MaxTableRows)
            return;
        model.PreviewRowsTotal = model.Rows.Count;
        model.PreviewRowLimit = ReportPreviewLimits.MaxTableRows;
        model.Rows = model.Rows.Take(ReportPreviewLimits.MaxTableRows).ToList();
    }

    internal static string F1(double v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Число этапов <c>List_item</c> по каждому <c>id_appointment</c> (C для одно-/многоэтапных талонов).</summary>
    internal static Dictionary<int, int> CountListItemsPerAppointment(IEnumerable<int> listItemAppointmentIds) =>
        listItemAppointmentIds
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

    internal static string FormatMultiStageSharePercent(int single, int multi)
    {
        var total = single + multi;
        return total == 0 ? "0" : F1(multi * 100.0 / total);
    }

    /// <summary>Подпись дня на оси X диаграмм отчётов каталога.</summary>
    internal static string FormatChartDayLabel(DateOnly day) =>
        day.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// В отчётах каталога <b>неявка на приём</b> — запись <c>Appointment</c> без ни одной строки <c>List_item</c>
    /// (в выборке по <paramref name="appointmentIds"/> нет <paramref name="listItemAppointmentIds"/>).
    /// Классификация этапов по времени не использует имя статуса «неяв» как синоним этой неявки.
    /// </summary>
    internal static int CountAppointmentsWithoutListItems(
        IReadOnlyCollection<int> appointmentIds,
        IEnumerable<int> listItemAppointmentIds)
    {
        var with = listItemAppointmentIds.ToHashSet();
        return appointmentIds.Count(id => !with.Contains(id));
    }

    /// <summary>
    /// Незавершённый фактический маршрут: есть этапы и хотя бы у одного нет <c>time_end_servicing</c>
    /// (как в отчёте «Поступило и завершено»).
    /// </summary>
    internal static bool AppointmentHasIncompleteRoute(IReadOnlyList<TimeOnly?> timeEndServicingPerStage)
    {
        if (timeEndServicingPerStage.Count == 0)
            return false;
        return timeEndServicingPerStage.Any(t => !t.HasValue);
    }

    /// <summary>Класс проблемного этапа по arrived-and-completed §4.5–4.6.</summary>
    internal enum StageIssueKind
    {
        NoCallNoEnd,
        NoEndWithActivity
    }

    /// <summary>
    /// §4.5: пусты <c>time_call</c> и <c>time_end_servicing</c>.
    /// §4.6: пуст <c>time_end_servicing</c>, задан <c>time_call</c> или <c>time_start_servicing</c>.
    /// </summary>
    internal static StageIssueKind? ClassifyStageIssue(
        TimeOnly? timeCall,
        TimeOnly? timeStartServicing,
        TimeOnly? timeEndServicing)
    {
        if (timeEndServicing.HasValue)
            return null;

        if (!timeCall.HasValue && !timeStartServicing.HasValue)
            return StageIssueKind.NoCallNoEnd;

        if (timeCall.HasValue || timeStartServicing.HasValue)
            return StageIssueKind.NoEndWithActivity;

        return null;
    }

    /// <summary>Порядок этапов маршрута: <c>time_start_servicing</c> ↑; пустой start — последние.</summary>
    internal static List<T> OrderStagesByStart<T>(IEnumerable<T> stages, Func<T, TimeOnly?> getTimeStart) =>
        stages.OrderBy(s => getTimeStart(s) ?? TimeOnly.MaxValue).ToList();

    internal static string FormatProblemSharePercent(int appointmentsCount, int withProblemCount) =>
        appointmentsCount <= 0
            ? "—"
            : F1(withProblemCount * 100.0 / appointmentsCount);
}
