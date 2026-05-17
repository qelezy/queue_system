using System.Globalization;
using WebApplication.Models;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Reports.Intervals;

namespace WebApplication.Services.Reports.LoadAndDowntime;

internal static class LoadAndDowntimeReportBuilder
{
    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<LogWorkLite> rawLogs,
        IReadOnlyList<ListRowLite> listRows,
        IReadOnlyDictionary<int, string> doctors,
        IReadOnlyDictionary<int, string> cabinets,
        DateTime periodFrom,
        DateTime periodTo,
        bool byCabinet,
        ReportGenerationPurpose purpose)
    {
        var logKeys = rawLogs
            .Select(l => (l.IdDoctor, l.IdCabinet, l.DateWork))
            .ToHashSet();

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

        return BuildResult(
            byCabinet,
            shifts,
            listRows,
            doctors,
            cabinets,
            periodFrom,
            periodTo,
            logKeys,
            purpose);
    }

    private static int CountDistinctCompletedAppointments(
        IReadOnlyList<ListRowLite> listRows,
        IEnumerable<(int Doc, int Cab, DateOnly Date)> keys,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys,
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
        ReportGenerationPurpose purpose)
    {
        const int colCount = 12;
        var headers = LoadDowntimeColumnHeaders(byCabinet);

        var rows = new List<ReportResultRowViewModel>();

        if (shifts.Count == 0)
        {
            rows.Add(PadRow(colCount, "Нет данных", "Нет Log_work с time_begin/time_end в периоде."));
            return new ReportResultViewModel
            {
                ColumnHeaders = headers.ToList(),
                Rows = rows,
                PreviewPieChart = null,
                PreviewCharts = null
            };
        }

        var previewPie = BuildLoadDowntimePreviewPieChart(shifts);

        if (purpose == ReportGenerationPurpose.ExportOrFull)
        {
            foreach (var row in BuildDetailRows(
                         byCabinet,
                         shifts,
                         listRows,
                         doctors,
                         cabinets,
                         periodFrom,
                         periodTo,
                         logKeys))
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
                        logKeys));
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
                        logKeys));
            }
        }
        else
        {
            const int previewTailReserved = 2;
            var maxBeforeTail = Math.Max(0, ReportPreviewLimits.MaxTableRows - previewTailReserved);
            var truncated = false;
            foreach (var row in BuildDetailRows(
                         byCabinet,
                         shifts,
                         listRows,
                         doctors,
                         cabinets,
                         periodFrom,
                         periodTo,
                         logKeys))
            {
                if (rows.Count >= maxBeforeTail)
                {
                    truncated = true;
                    break;
                }

                rows.Add(row);
            }

            if (truncated)
            {
                rows.Add(PadRow(
                    colCount,
                    "…",
                    "Показаны не все строки; полный отчёт — при сохранении в файл.",
                    rowClass: "report-load-table__row--preview-truncated-hint"));
                rows.Add(BuildGrandPeriodTotalRowForPreview(
                    byCabinet,
                    shifts,
                    listRows,
                    periodFrom,
                    periodTo,
                    logKeys));
            }
            else
            {
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
                            logKeys));
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
                            logKeys));
                }
            }
        }

        return new ReportResultViewModel
        {
            ColumnHeaders = headers.ToList(),
            Rows = rows,
            PreviewPieChart = previewPie,
            PreviewCharts = ReportPreviewChartDescriptors.ForLoadDowntimePie(previewPie)
        };
    }

    private static IReadOnlyList<string> LoadDowntimeColumnHeaders(bool cabinetColumnOrder)
    {
        var metricTail = new[]
        {
            "Длительность рабочего времени, мин",
            "Общая длительность обслуживания, мин",
            "Общая длительность простоя, мин",
            "Средняя длительность простоя, мин",
            "Число интервалов простоя",
            "Загрузка рабочего времени, %",
            "Число завершённых приёмов"
        };

        if (cabinetColumnOrder)
        {
            return new[]
            {
                "Дата",
                "Интервал работы",
                "Кабинет",
                "Врач",
                "Специализация врача"
            }.Concat(metricTail).ToList();
        }

        return new[]
        {
            "Дата",
            "Интервал работы",
            "Врач",
            "Специализация врача",
            "Кабинет"
        }.Concat(metricTail).ToList();
    }

    private static List<string> BuildLoadDowntimeDetailCells(
        bool cabinetColumnOrder,
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
        var cab = string.IsNullOrEmpty(cabinetCell) ? "—" : cabinetCell;
        var loadPct = windowMin <= 0
            ? "—"
            : Math.Round(busyMin * 100.0 / windowMin, 1).ToString(CultureInfo.InvariantCulture);
        var idleAvg = idleSegments <= 0
            ? "—"
            : Math.Round(idleMin / idleSegments, 1).ToString(CultureInfo.InvariantCulture);

        var tail = new[]
        {
            Math.Round(windowMin, 1).ToString(CultureInfo.InvariantCulture),
            Math.Round(busyMin, 1).ToString(CultureInfo.InvariantCulture),
            Math.Round(idleMin, 1).ToString(CultureInfo.InvariantCulture),
            idleAvg,
            idleSegments.ToString(CultureInfo.InvariantCulture),
            loadPct,
            completedAppointments.ToString(CultureInfo.InvariantCulture)
        };

        if (cabinetColumnOrder)
            return [dateCell, timeCell, cab, doctorCell, specialtiesCell, ..tail];

        return [dateCell, timeCell, doctorCell, specialtiesCell, cab, ..tail];
    }

    private static ReportResultRowViewModel BuildGrandPeriodTotalRowForPreview(
        bool byCabinet,
        IReadOnlyList<ShiftMetrics> shifts,
        IReadOnlyList<ListRowLite> listRows,
        DateTime periodFrom,
        DateTime periodTo,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys)
    {
        var w = shifts.Sum(x => x.WindowMinutes);
        var b = shifts.Sum(x => x.BusyMinutes);
        var i = shifts.Sum(x => x.IdleMinutes);
        var seg = shifts.Sum(x => x.IdleSegments);
        var shiftKeys = shifts.Select(s => (s.IdDoctor, s.IdCabinet, s.DateWork)).Distinct().ToList();
        var specCell = byCabinet
            ? "—"
            : FormatSpecialtyListForKeys(listRows, shiftKeys, periodFrom, periodTo);
        var apptCount = CountDistinctCompletedAppointments(
            listRows,
            shiftKeys,
            logKeys,
            periodFrom,
            periodTo);

        var docCell = byCabinet ? "—" : "Все врачи";
        var cabCell = byCabinet ? "Все кабинеты" : "—";

        return new ReportResultRowViewModel
        {
            Cells = BuildLoadDowntimeDetailCells(
                byCabinet,
                "",
                "—",
                docCell,
                specCell,
                cabCell,
                w,
                b,
                i,
                seg,
                apptCount),
            RowClass = "report-load-table__row--period-total"
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
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys)
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
                        markDayTotalsEnd: di == dayDoctorGroups.Count - 1);
                }
            }
        }
        else
        {
            foreach (var day in shifts.Select(s => s.DateWork).Distinct().OrderBy(d => d))
            {
                var cabinetGroupsThisDay = shifts
                    .Where(s => s.DateWork == day)
                    .GroupBy(s => (s.IdCabinet, s.DateWork))
                    .OrderBy(gr => cabinets.GetValueOrDefault(gr.Key.IdCabinet, ""), StringComparer.Ordinal)
                    .ToList();

                foreach (var g in cabinetGroupsThisDay)
                {
                    foreach (var s in g
                                 .OrderBy(x => doctors.GetValueOrDefault(x.IdDoctor, ""), StringComparer.Ordinal)
                                 .ThenBy(x => x.IdDoctor))
                    {
                        var dateCell = prevDate == g.Key.DateWork
                            ? ""
                            : g.Key.DateWork.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        prevDate = g.Key.DateWork;
                        var key = new[] { (s.IdDoctor, s.IdCabinet, s.DateWork) };
                        yield return new ReportResultRowViewModel
                        {
                            Cells = BuildLoadDowntimeDetailCells(
                                true,
                                dateCell,
                                FormatDaySpan(s.DaySpanStart, s.DaySpanEnd),
                                doctors.GetValueOrDefault(s.IdDoctor, "?"),
                                FormatSpecialtyListForKeys(listRows, key, periodFrom, periodTo),
                                "Каб. " + cabinets.GetValueOrDefault(g.Key.IdCabinet, "?"),
                                s.WindowMinutes,
                                s.BusyMinutes,
                                s.IdleMinutes,
                                s.IdleSegments,
                                s.CompletedAppointments)
                        };
                    }
                }

                yield return PadRow(
                    12,
                    "Итого за день",
                    "",
                    rowClass: "report-load-table__row--day-totals-heading",
                    cellColSpans: LoadDowntimeTotalsLabelColSpans);

                for (var ci = 0; ci < cabinetGroupsThisDay.Count; ci++)
                {
                    yield return DayCabinetTotalDataRow(
                        cabinetGroupsThisDay[ci],
                        listRows,
                        cabinets,
                        periodFrom,
                        periodTo,
                        logKeys,
                        markDayTotalsEnd: ci == cabinetGroupsThisDay.Count - 1);
                }
            }
        }
    }

    private static ReportResultRowViewModel DayDoctorTotalDataRow(
        IGrouping<(int IdDoctor, DateOnly DateWork), ShiftMetrics> g,
        IReadOnlyList<ListRowLite> listRows,
        IReadOnlyDictionary<int, string> doctors,
        DateTime periodFrom,
        DateTime periodTo,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys,
        bool markDayTotalsEnd)
    {
        var w = g.Sum(x => x.WindowMinutes);
        var b = g.Sum(x => x.BusyMinutes);
        var i = g.Sum(x => x.IdleMinutes);
        var seg = g.Sum(x => x.IdleSegments);
        var shiftKeys = g.Select(s => (s.IdDoctor, s.IdCabinet, s.DateWork)).Distinct().ToList();
        var specCell = FormatSpecialtyListForKeys(listRows, shiftKeys, periodFrom, periodTo);
        var apptCount = CountDistinctCompletedAppointments(
            listRows,
            shiftKeys,
            logKeys,
            periodFrom,
            periodTo);

        return new ReportResultRowViewModel
        {
            Cells = BuildLoadDowntimeDetailCells(
                false,
                "",
                "—",
                doctors.GetValueOrDefault(g.Key.IdDoctor, "?"),
                specCell,
                "—",
                w,
                b,
                i,
                seg,
                apptCount),
            RowClass = markDayTotalsEnd
                ? "report-load-table__row--day-doctor-total report-load-table__row--day-totals-end"
                : "report-load-table__row--day-doctor-total",
        };
    }

    private static ReportResultRowViewModel DayCabinetTotalDataRow(
        IGrouping<(int IdCabinet, DateOnly DateWork), ShiftMetrics> g,
        IReadOnlyList<ListRowLite> listRows,
        IReadOnlyDictionary<int, string> cabinets,
        DateTime periodFrom,
        DateTime periodTo,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys,
        bool markDayTotalsEnd)
    {
        var w = g.Sum(x => x.WindowMinutes);
        var b = g.Sum(x => x.BusyMinutes);
        var i = g.Sum(x => x.IdleMinutes);
        var seg = g.Sum(x => x.IdleSegments);
        var shiftKeys = g.Select(s => (s.IdDoctor, s.IdCabinet, s.DateWork)).Distinct().ToList();
        var apptCount = CountDistinctCompletedAppointments(
            listRows,
            shiftKeys,
            logKeys,
            periodFrom,
            periodTo);

        return new ReportResultRowViewModel
        {
            Cells = BuildLoadDowntimeDetailCells(
                true,
                "",
                "—",
                "—",
                "—",
                "Каб. " + cabinets.GetValueOrDefault(g.Key.IdCabinet, "?"),
                w,
                b,
                i,
                seg,
                apptCount),
            RowClass = markDayTotalsEnd
                ? "report-load-table__row--day-cabinet-total report-load-table__row--day-totals-end"
                : "report-load-table__row--day-cabinet-total",
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
        int completedAppointments) =>
        new ReportResultRowViewModel
        {
            Cells = BuildLoadDowntimeDetailCells(
                false,
                dateCell,
                timeCell,
                doctorCell,
                specialtiesCell,
                cabinetCell,
                windowMin,
                busyMin,
                idleMin,
                idleSegments,
                completedAppointments)
        };

    private static ReportResultRowViewModel AggregateToRow(
        IGrouping<int, ShiftMetrics> grp,
        IReadOnlyList<ListRowLite> listRows,
        IReadOnlyDictionary<int, string> doctors,
        IReadOnlyDictionary<int, string> cabinets,
        bool byCabinet,
        DateTime periodFrom,
        DateTime periodTo,
        HashSet<(int IdDoctor, int IdCabinet, DateOnly Date)> logKeys)
    {
        var w = grp.Sum(x => x.WindowMinutes);
        var b = grp.Sum(x => x.BusyMinutes);
        var i = grp.Sum(x => x.IdleMinutes);
        var seg = grp.Sum(x => x.IdleSegments);

        var shiftKeys = grp.Select(s => (s.IdDoctor, s.IdCabinet, s.DateWork)).ToList();
        var specCell = byCabinet
            ? "—"
            : FormatSpecialtyListForKeys(listRows, shiftKeys, periodFrom, periodTo);
        var apptCount = CountDistinctCompletedAppointments(listRows, shiftKeys, logKeys, periodFrom, periodTo);

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
            Cells = BuildLoadDowntimeDetailCells(
                byCabinet,
                dateCell,
                timeCell,
                docCell,
                specCell,
                cabCell,
                w,
                b,
                i,
                seg,
                apptCount),
            RowClass = "report-load-table__row--period-total"
        };
    }

    internal sealed record LogWorkLite(int IdDoctor, int IdCabinet, DateOnly DateWork, TimeOnly TimeBegin, TimeOnly TimeEnd);

    internal sealed record ListRowLite(
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
