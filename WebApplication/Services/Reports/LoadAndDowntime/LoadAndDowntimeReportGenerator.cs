using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Reports.Intervals;

namespace WebApplication.Services.Reports.LoadAndDowntime;

public sealed class LoadAndDowntimeReportGenerator : IReportGenerator
{
    public string ReportId => ReportIds.DoctorCabinetLoadDowntime;

    public ReportGenerateResponse Generate(ReportGenerateRequest request, ElectronicQueueDbContext queue)
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

        var byCabinet = request.CustomParams is not null
                        && request.CustomParams.TryGetValue("analysisMode", out var am)
                        && string.Equals(am?.Trim(), "cabinet", StringComparison.OrdinalIgnoreCase);

        var fromDo = DateOnly.FromDateTime(periodFrom);
        var toDoOnly = DateOnly.FromDateTime(periodTo);

        var noShowStatusIds = queue.StatusItemLists.AsNoTracking()
            .Where(s => QueueDashboardStatusMapper.IsNoShowStatusName(s.Name))
            .Select(s => s.IdStatusItem)
            .ToHashSet();

        var rawLogs = queue.LogWorks.AsNoTracking()
            .Where(lw => lw.TimeBegin != null && lw.TimeEnd != null
                         && lw.DateWork >= fromDo && lw.DateWork <= toDoOnly)
            .Select(lw => new LogWorkLite(
                lw.IdDoctor,
                lw.IdCabinet,
                lw.DateWork,
                lw.TimeBegin!.Value,
                lw.TimeEnd!.Value))
            .ToList();

        var logKeys = rawLogs
            .Select(l => (l.IdDoctor, l.IdCabinet, l.DateWork))
            .ToHashSet();

        var listRows = (
            from li in queue.ListItems.AsNoTracking()
            join a in queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            join st in queue.StatusItemLists.AsNoTracking() on li.IdStatusItem equals st.IdStatusItem
            join sp in queue.Specialties.AsNoTracking() on li.IdSpecialty equals sp.IdSpecialty
            where a.DateArrival >= fromDo && a.DateArrival <= toDoOnly
                  && li.TimeStartServicing != null
                  && li.TimeEndServicing != null
            select new ListRowLite(
                li.IdAppointment,
                li.IdDoctor,
                li.IdCabinet,
                a.DateArrival,
                li.IdStatusItem,
                st.Name,
                li.TimeCall,
                li.TimeStartServicing!.Value,
                li.TimeEndServicing!.Value,
                sp.Definition))
            .ToList();

        var doctors = queue.Doctors.AsNoTracking().ToDictionary(d => d.IdDoctor, d => d.FullName);
        var cabinets = queue.Cabinets.AsNoTracking().ToDictionary(c => c.IdCabinet, c => c.CabinetNumber);

        var shifts = new List<ShiftMetrics>();

        foreach (var g in rawLogs.GroupBy(x => (x.IdDoctor, x.IdCabinet, x.DateWork)))
        {
            var windows = IntervalOperations.MergeOverlapping(
                g.Select(lw =>
                    {
                        var raw = new DateTimeInterval(
                            lw.DateWork.ToDateTime(lw.TimeBegin),
                            lw.DateWork.ToDateTime(lw.TimeEnd));
                        return IntervalOperations.ClipToRange(raw, periodFrom, periodTo);
                    })
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value));

            if (windows.Count == 0)
                continue;

            var daySpanStart = windows.Min(w => w.Start);
            var daySpanEnd = windows.Max(w => w.End);

            var busyRaw = new List<DateTimeInterval>();
            foreach (var row in listRows)
            {
                if (row.IdDoctor != g.Key.IdDoctor || row.IdCabinet != g.Key.IdCabinet || row.DateArrival != g.Key.DateWork)
                    continue;
                if (noShowStatusIds.Contains(row.IdStatusItem) || QueueDashboardStatusMapper.IsNoShowStatusName(row.StatusName))
                    continue;

                busyRaw.Add(new DateTimeInterval(
                    EqDateTimeExtensions.CombineOnArrivalDate(row.DateArrival, row.TimeStart),
                    EqDateTimeExtensions.CombineOnArrivalDate(row.DateArrival, row.TimeEnd)));
            }

            var mergedBusy = IntervalOperations.MergeOverlapping(busyRaw);

            double windowMin = 0;
            double busyMin = 0;
            double idleMin = 0;
            var idleSeg = 0;

            foreach (var w in windows)
            {
                var busyInW = IntervalOperations.MergeOverlapping(
                    mergedBusy
                        .Select(b => IntervalOperations.Intersect(b, w))
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value));

                windowMin += w.Duration.TotalMinutes;
                busyMin += IntervalOperations.TotalMinutes(busyInW);
                var idleParts = IntervalOperations.SubtractUnionFromWindow(w, busyInW);
                idleMin += IntervalOperations.TotalMinutes(idleParts);
                idleSeg += idleParts.Count;
            }

            var shiftKey = new[] { (g.Key.IdDoctor, g.Key.IdCabinet, g.Key.DateWork) };
            var completedAppointments = CountDistinctCompletedAppointments(
                listRows,
                shiftKey,
                logKeys,
                noShowStatusIds,
                periodFrom,
                periodTo);

            shifts.Add(new ShiftMetrics(
                g.Key.IdDoctor,
                g.Key.IdCabinet,
                g.Key.DateWork,
                daySpanStart,
                daySpanEnd,
                windowMin,
                busyMin,
                idleMin,
                idleSeg,
                completedAppointments));
        }

        shifts.Sort(CompareShifts(doctors, cabinets));

        var result = BuildResult(
            byCabinet,
            shifts,
            listRows,
            doctors,
            cabinets,
            periodFrom,
            periodTo,
            logKeys,
            noShowStatusIds);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = result };
    }

    private static int CountDistinctCompletedAppointments(
        IReadOnlyList<ListRowLite> listRows,
        IEnumerable<(int Doc, int Cab, DateOnly Date)> keys,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys,
        HashSet<int> noShowStatusIds,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var keySet = keys.ToHashSet();
        var ids = new HashSet<int>();
        foreach (var row in listRows)
        {
            if (!keySet.Contains((row.IdDoctor, row.IdCabinet, row.DateArrival)))
                continue;
            if (row.TimeCall is null)
                continue;
            if (noShowStatusIds.Contains(row.IdStatusItem) || QueueDashboardStatusMapper.IsNoShowStatusName(row.StatusName))
                continue;
            if (!logKeys.Contains((row.IdDoctor, row.IdCabinet, row.DateArrival)))
                continue;
            if (!ServicingIntersectsPeriod(row, periodFrom, periodTo))
                continue;
            var endDt = EqDateTimeExtensions.CombineOnArrivalDate(row.DateArrival, row.TimeEnd);
            if (endDt < periodFrom || endDt > periodTo)
                continue;
            ids.Add(row.IdAppointment);
        }

        return ids.Count;
    }

    private static string FormatDaySpan(DateTime start, DateTime end) =>
        $"{start.ToString("HH:mm", CultureInfo.InvariantCulture)}–{end.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    private static bool ServicingIntersectsPeriod(ListRowLite r, DateTime periodFrom, DateTime periodTo)
    {
        var start = EqDateTimeExtensions.CombineOnArrivalDate(r.DateArrival, r.TimeStart);
        var end = EqDateTimeExtensions.CombineOnArrivalDate(r.DateArrival, r.TimeEnd);
        return end >= periodFrom && start <= periodTo;
    }

    private static string FormatSpecialtyListForKeys(
        IReadOnlyList<ListRowLite> rows,
        IReadOnlyCollection<(int Doc, int Cab, DateOnly Date)> keys,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var keySet = keys.ToHashSet();
        var parts = rows
            .Where(r => keySet.Contains((r.IdDoctor, r.IdCabinet, r.DateArrival)))
            .Where(r => ServicingIntersectsPeriod(r, periodFrom, periodTo))
            .Select(r => r.SpecialtyDefinition?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        return parts.Count == 0 ? "—" : string.Join("; ", parts);
    }

    private static Comparison<ShiftMetrics> CompareShifts(
        IReadOnlyDictionary<int, string> doctors,
        IReadOnlyDictionary<int, string> cabinets) =>
        (a, b) =>
        {
            var c = a.DateWork.CompareTo(b.DateWork);
            if (c != 0)
                return c;
            c = string.CompareOrdinal(doctors.GetValueOrDefault(a.IdDoctor, ""), doctors.GetValueOrDefault(b.IdDoctor, ""));
            if (c != 0)
                return c;
            return string.CompareOrdinal(cabinets.GetValueOrDefault(a.IdCabinet, ""), cabinets.GetValueOrDefault(b.IdCabinet, ""));
        };

    private static ReportResultViewModel BuildResult(
        bool byCabinet,
        List<ShiftMetrics> shifts,
        IReadOnlyList<ListRowLite> listRows,
        IReadOnlyDictionary<int, string> doctors,
        IReadOnlyDictionary<int, string> cabinets,
        DateTime periodFrom,
        DateTime periodTo,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys,
        HashSet<int> noShowStatusIds)
    {
        const int colCount = 12;
        var headers = new List<string>
        {
            "Дата",
            "Интервал работы",
            "Врач",
            "Специализация врача",
            "Кабинет",
            "Длительность смены, мин",
            "Общая длительность обслуживания, мин",
            "Общая длительность простоя, мин",
            "Средняя длительность простоя, мин",
            "Число интервалов простоя",
            "Загрузка рабочего времени, %",
            "Число завершённых приёмов"
        };

        var rows = new List<ReportResultRowViewModel>();

        if (shifts.Count == 0)
        {
            rows.Add(PadRow(colCount, "Нет данных", "Нет Log_work с time_begin/time_end в периоде."));
            return new ReportResultViewModel
            {
                GeneratedForReportId = ReportIds.DoctorCabinetLoadDowntime,
                Title = "Загрузка и простои",
                DownloadFileName = "load-and-downtime.csv",
                ColumnHeaders = headers,
                Rows = rows,
                PreviewPieChart = null
            };
        }

        foreach (var row in BuildDetailRows(
                     byCabinet,
                     shifts,
                     listRows,
                     doctors,
                     cabinets,
                     periodFrom,
                     periodTo,
                     logKeys,
                     noShowStatusIds))
            rows.Add(row);

        rows.Add(PadRow(
            colCount,
            byCabinet ? "Итого по кабинетам" : "Итого по врачам",
            "",
            rowClass: "report-load-table__row--totals-start",
            cellColSpans: LoadDowntimeTotalsLabelColSpans));

        if (byCabinet)
        {
            foreach (var grp in shifts.GroupBy(x => x.IdCabinet).OrderBy(g => cabinets.GetValueOrDefault(g.Key, "")))
                rows.Add(AggregateToRow(
                    grp,
                    listRows,
                    doctors,
                    cabinets,
                    byCabinet: true,
                    periodFrom,
                    periodTo,
                    logKeys,
                    noShowStatusIds));
        }
        else
        {
            foreach (var grp in shifts.GroupBy(x => x.IdDoctor).OrderBy(g => doctors.GetValueOrDefault(g.Key, "")))
                rows.Add(AggregateToRow(
                    grp,
                    listRows,
                    doctors,
                    cabinets,
                    byCabinet: false,
                    periodFrom,
                    periodTo,
                    logKeys,
                    noShowStatusIds));
        }

        return new ReportResultViewModel
        {
            GeneratedForReportId = ReportIds.DoctorCabinetLoadDowntime,
            Title = "Загрузка и простои",
            DownloadFileName = "load-and-downtime.csv",
            ColumnHeaders = headers,
            Rows = rows,
            PreviewPieChart = BuildLoadDowntimePreviewPieChart(shifts)
        };
    }

    private static ReportPreviewPieChart? BuildLoadDowntimePreviewPieChart(IReadOnlyList<ShiftMetrics> shifts)
    {
        if (shifts.Count == 0)
            return null;
        var busy = shifts.Sum(s => s.BusyMinutes);
        var idle = shifts.Sum(s => s.IdleMinutes);
        if (busy <= 0 && idle <= 0)
            return null;
        return new ReportPreviewPieChart
        {
            Labels = ["Обслуживание (мин)", "Простой (мин)"],
            Values = [busy, idle]
        };
    }

    private static readonly List<int> LoadDowntimeTotalsLabelColSpans =
        [2, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1];

    private static ReportResultRowViewModel PadRow(
        int colCount,
        string c0,
        string c1,
        string? rowClass = null,
        IReadOnlyList<int>? cellColSpans = null)
    {
        var cells = new List<string> { c0, c1 };
        while (cells.Count < colCount)
            cells.Add("");
        return new ReportResultRowViewModel
        {
            Cells = cells,
            RowClass = rowClass,
            CellColSpans = cellColSpans is null ? null : cellColSpans.ToList()
        };
    }

    private static IEnumerable<ReportResultRowViewModel> BuildDetailRows(
        bool byCabinet,
        List<ShiftMetrics> shifts,
        IReadOnlyList<ListRowLite> listRows,
        IReadOnlyDictionary<int, string> doctors,
        IReadOnlyDictionary<int, string> cabinets,
        DateTime periodFrom,
        DateTime periodTo,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys,
        HashSet<int> noShowStatusIds)
    {
        DateOnly? prevDate = null;
        if (!byCabinet)
        {
            foreach (var day in shifts.Select(s => s.DateWork).Distinct().OrderBy(d => d))
            {
                var dayShifts = shifts
                    .Where(s => s.DateWork == day)
                    .OrderBy(s => doctors.GetValueOrDefault(s.IdDoctor, ""), StringComparer.Ordinal)
                    .ThenBy(s => cabinets.GetValueOrDefault(s.IdCabinet, ""), StringComparer.Ordinal)
                    .ToList();

                foreach (var s in dayShifts)
                {
                    var dateCell = prevDate == s.DateWork
                        ? ""
                        : s.DateWork.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    prevDate = s.DateWork;
                    var key = new[] { (s.IdDoctor, s.IdCabinet, s.DateWork) };
                    yield return DetailGroupToRow(
                        dateCell,
                        FormatDaySpan(s.DaySpanStart, s.DaySpanEnd),
                        doctors.GetValueOrDefault(s.IdDoctor, "?"),
                        FormatSpecialtyListForKeys(listRows, key, periodFrom, periodTo),
                        "Каб. " + cabinets.GetValueOrDefault(s.IdCabinet, "?"),
                        s.WindowMinutes,
                        s.BusyMinutes,
                        s.IdleMinutes,
                        s.IdleSegments,
                        s.CompletedAppointments);
                }

                yield return PadRow(
                    12,
                    "Итого за день",
                    "",
                    rowClass: "report-load-table__row--day-totals-heading",
                    cellColSpans: LoadDowntimeTotalsLabelColSpans);

                var dayDoctorGroups = dayShifts
                    .GroupBy(s => (s.IdDoctor, s.DateWork))
                    .OrderBy(gr => doctors.GetValueOrDefault(gr.Key.IdDoctor, ""), StringComparer.Ordinal)
                    .ToList();

                for (var di = 0; di < dayDoctorGroups.Count; di++)
                {
                    yield return DayDoctorTotalDataRow(
                        dayDoctorGroups[di],
                        listRows,
                        doctors,
                        periodFrom,
                        periodTo,
                        logKeys,
                        noShowStatusIds,
                        markDayTotalsEnd: di == dayDoctorGroups.Count - 1);
                }
            }
        }
        else
        {
            foreach (var g in shifts
                         .GroupBy(s => (s.IdCabinet, s.DateWork))
                         .OrderBy(x => x.Key.DateWork)
                         .ThenBy(x => cabinets.GetValueOrDefault(x.Key.IdCabinet, "")))
            {
                var dateCell = prevDate == g.Key.DateWork
                    ? ""
                    : g.Key.DateWork.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                prevDate = g.Key.DateWork;
                var mergedKeys = g.Select(s => (s.IdDoctor, s.IdCabinet, s.DateWork)).Distinct().ToList();
                var timeCell = FormatDaySpan(g.Min(s => s.DaySpanStart), g.Max(s => s.DaySpanEnd));
                var apptCount = CountDistinctCompletedAppointments(
                    listRows,
                    mergedKeys,
                    logKeys,
                    noShowStatusIds,
                    periodFrom,
                    periodTo);
                yield return DetailGroupToRow(
                    dateCell,
                    timeCell,
                    FormatDoctorList(g, doctors),
                    FormatSpecialtyListForKeys(listRows, mergedKeys, periodFrom, periodTo),
                    "Каб. " + cabinets.GetValueOrDefault(g.Key.IdCabinet, "?"),
                    g.Sum(s => s.WindowMinutes),
                    g.Sum(s => s.BusyMinutes),
                    g.Sum(s => s.IdleMinutes),
                    g.Sum(s => s.IdleSegments),
                    apptCount);
            }
        }
    }

    private static string FormatDoctorList(
        IGrouping<(int IdCabinet, DateOnly DateWork), ShiftMetrics> g,
        IReadOnlyDictionary<int, string> doctors) =>
        string.Join(
            "; ",
            g.Select(s => doctors.GetValueOrDefault(s.IdDoctor, "?"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal));

    private static ReportResultRowViewModel DayDoctorTotalDataRow(
        IGrouping<(int IdDoctor, DateOnly DateWork), ShiftMetrics> g,
        IReadOnlyList<ListRowLite> listRows,
        IReadOnlyDictionary<int, string> doctors,
        DateTime periodFrom,
        DateTime periodTo,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys,
        HashSet<int> noShowStatusIds,
        bool markDayTotalsEnd)
    {
        var w = g.Sum(x => x.WindowMinutes);
        var b = g.Sum(x => x.BusyMinutes);
        var i = g.Sum(x => x.IdleMinutes);
        var seg = g.Sum(x => x.IdleSegments);
        var loadPct = w <= 0 ? "—" : Math.Round(b * 100.0 / w, 1).ToString(CultureInfo.InvariantCulture);
        var idleAvg = seg <= 0 ? "—" : Math.Round(i / seg, 1).ToString(CultureInfo.InvariantCulture);
        var shiftKeys = g.Select(s => (s.IdDoctor, s.IdCabinet, s.DateWork)).Distinct().ToList();
        var specCell = FormatSpecialtyListForKeys(listRows, shiftKeys, periodFrom, periodTo);
        var apptCount = CountDistinctCompletedAppointments(
            listRows,
            shiftKeys,
            logKeys,
            noShowStatusIds,
            periodFrom,
            periodTo);

        return new ReportResultRowViewModel
        {
            Cells =
            [
                "",
                "—",
                doctors.GetValueOrDefault(g.Key.IdDoctor, "?"),
                specCell,
                "—",
                Math.Round(w, 1).ToString(CultureInfo.InvariantCulture),
                Math.Round(b, 1).ToString(CultureInfo.InvariantCulture),
                Math.Round(i, 1).ToString(CultureInfo.InvariantCulture),
                idleAvg,
                seg.ToString(CultureInfo.InvariantCulture),
                loadPct,
                apptCount.ToString(CultureInfo.InvariantCulture)
            ],
            RowClass = markDayTotalsEnd ? "report-load-table__row--day-totals-end" : null
        };
    }

    private static ReportResultRowViewModel DetailGroupToRow(
        string dateCell,
        string timeCell,
        string doctorCell,
        string specialtiesCell,
        string cabinetCell,
        double windowMin,
        double busyMin,
        double idleMin,
        int idleSegments,
        int completedAppointments)
    {
        var loadPct = windowMin <= 0
            ? "—"
            : Math.Round(busyMin * 100.0 / windowMin, 1).ToString(CultureInfo.InvariantCulture);
        var idleAvg = idleSegments <= 0
            ? "—"
            : Math.Round(idleMin / idleSegments, 1).ToString(CultureInfo.InvariantCulture);

        return new ReportResultRowViewModel
        {
            Cells =
            [
                dateCell,
                timeCell,
                doctorCell,
                specialtiesCell,
                string.IsNullOrEmpty(cabinetCell) ? "—" : cabinetCell,
                Math.Round(windowMin, 1).ToString(CultureInfo.InvariantCulture),
                Math.Round(busyMin, 1).ToString(CultureInfo.InvariantCulture),
                Math.Round(idleMin, 1).ToString(CultureInfo.InvariantCulture),
                idleAvg,
                idleSegments.ToString(CultureInfo.InvariantCulture),
                loadPct,
                completedAppointments.ToString(CultureInfo.InvariantCulture)
            ]
        };
    }

    private static ReportResultRowViewModel AggregateToRow(
        IGrouping<int, ShiftMetrics> grp,
        IReadOnlyList<ListRowLite> listRows,
        IReadOnlyDictionary<int, string> doctors,
        IReadOnlyDictionary<int, string> cabinets,
        bool byCabinet,
        DateTime periodFrom,
        DateTime periodTo,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys,
        HashSet<int> noShowStatusIds)
    {
        var w = grp.Sum(x => x.WindowMinutes);
        var b = grp.Sum(x => x.BusyMinutes);
        var i = grp.Sum(x => x.IdleMinutes);
        var seg = grp.Sum(x => x.IdleSegments);

        var loadPct = w <= 0 ? "—" : Math.Round(b * 100.0 / w, 1).ToString(CultureInfo.InvariantCulture);
        var idleAvg = seg <= 0 ? "—" : Math.Round(i / seg, 1).ToString(CultureInfo.InvariantCulture);

        var shiftKeys = grp.Select(s => (s.IdDoctor, s.IdCabinet, s.DateWork)).ToList();
        var specCell = FormatSpecialtyListForKeys(listRows, shiftKeys, periodFrom, periodTo);
        var apptCount = CountDistinctCompletedAppointments(listRows, shiftKeys, logKeys, noShowStatusIds, periodFrom, periodTo);

        string dateCell = "";
        string timeCell = "—";
        string docCell;
        string cabCell;
        if (byCabinet)
        {
            docCell = "—";
            cabCell = "Каб. " + cabinets.GetValueOrDefault(grp.Key, "?");
        }
        else
        {
            docCell = doctors.GetValueOrDefault(grp.Key, "?");
            cabCell = "—";
        }

        return new ReportResultRowViewModel
        {
            Cells =
            [
                dateCell,
                timeCell,
                docCell,
                specCell,
                cabCell,
                Math.Round(w, 1).ToString(CultureInfo.InvariantCulture),
                Math.Round(b, 1).ToString(CultureInfo.InvariantCulture),
                Math.Round(i, 1).ToString(CultureInfo.InvariantCulture),
                idleAvg,
                seg.ToString(CultureInfo.InvariantCulture),
                loadPct,
                apptCount.ToString(CultureInfo.InvariantCulture)
            ]
        };
    }

    private sealed record LogWorkLite(int IdDoctor, int IdCabinet, DateOnly DateWork, TimeOnly TimeBegin, TimeOnly TimeEnd);

    private sealed record ListRowLite(
        int IdAppointment,
        int IdDoctor,
        int IdCabinet,
        DateOnly DateArrival,
        int IdStatusItem,
        string? StatusName,
        TimeOnly? TimeCall,
        TimeOnly TimeStart,
        TimeOnly TimeEnd,
        string? SpecialtyDefinition);

    private sealed record ShiftMetrics(
        int IdDoctor,
        int IdCabinet,
        DateOnly DateWork,
        DateTime DaySpanStart,
        DateTime DaySpanEnd,
        double WindowMinutes,
        double BusyMinutes,
        double IdleMinutes,
        int IdleSegments,
        int CompletedAppointments);
}
