using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplication.Data;
using WebApplication.Models;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services;

public sealed class QueueAnalyticsService : IQueueAnalyticsService
{
    private readonly ElectronicQueueDbContext _queue;
    private readonly MonitoringOptions _opt;

    public QueueAnalyticsService(ElectronicQueueDbContext queue, IOptions<MonitoringOptions> options)
    {
        _queue = queue;
        _opt = options.Value;
    }

    public async Task<ManagerAnalyticsViewModel> GetManagerAnalyticsAsync(
        DateOnly from,
        DateOnly to,
        int? cabinetId,
        int? doctorId,
        int? categoryId,
        bool heatmapByDoctor,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
            (from, to) = (to, from);

        var maxDays = Math.Max(1, _opt.ManagerMaxRangeDays);
        if ((to.DayNumber - from.DayNumber) > maxDays)
            to = from.AddDays(maxDays);

        var periodDayCount = (to.DayNumber - from.DayNumber) + 1;
        var hourLabels = BuildHourLabels();
        var hourCount = hourLabels.Count;

        var cabinetOpts = await _queue.Cabinets.AsNoTracking()
            .OrderBy(c => c.CabinetNumber)
            .Select(c => new SelectOptionViewModel { Value = c.IdCabinet, Text = $"Каб. {c.CabinetNumber}" })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        cabinetOpts.Insert(0, new SelectOptionViewModel { Value = null, Text = "Все кабинеты" });

        var doctorOpts = await _queue.Doctors.AsNoTracking()
            .OrderBy(d => d.FullName)
            .Select(d => new SelectOptionViewModel { Value = d.IdDoctor, Text = d.FullName })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        doctorOpts.Insert(0, new SelectOptionViewModel { Value = null, Text = "Все врачи" });

        var catOpts = await _queue.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectOptionViewModel { Value = c.IdCategory, Text = c.Name })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        catOpts.Insert(0, new SelectOptionViewModel { Value = null, Text = "Все категории" });

        var doctorNameMap = await _queue.Doctors.AsNoTracking()
            .ToDictionaryAsync(d => d.IdDoctor, d => d.FullName, cancellationToken).ConfigureAwait(false);
        var cabinetLabelMap = await _queue.Cabinets.AsNoTracking()
            .ToDictionaryAsync(c => c.IdCabinet, c => c.CabinetNumber, cancellationToken).ConfigureAwait(false);

        var apptQuery = _queue.Appointments.AsNoTracking()
            .Where(a => a.DateArrival >= from && a.DateArrival <= to);

        if (categoryId is int cid and > 0)
            apptQuery = apptQuery.Where(a => a.IdCategory == cid);

        var appointments = await apptQuery
            .Include(a => a.ListItems)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var completedStages = ExtractCompletedStages(appointments, cabinetId, doctorId);
        var waitHistogram = BuildHistogram(completedStages);
        var avgWaitByDoctor = BuildDoctorAggregates(completedStages, s => s.WaitMinutes, doctorNameMap, _opt.HeatmapTopN);
        var avgServiceByDoctor = BuildDoctorAggregates(completedStages, s => s.ServiceMinutes, doctorNameMap, _opt.HeatmapTopN);

        var queueByHourPerDay = new List<ManagerDaySeriesViewModel>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var dayAppts = appointments.Where(a => a.DateArrival == d).ToList();
            var counts = new int[hourCount];
            for (var hi = 0; hi < hourCount; hi++)
            {
                var h = _opt.WorkdayStartHour + hi;
                var slotEnd = d.ToDateTime(new TimeOnly(h, 59, 59));
                counts[hi] = CountWaitingAt(dayAppts, slotEnd, cabinetId, doctorId, categoryId);
            }

            queueByHourPerDay.Add(new ManagerDaySeriesViewModel
            {
                DayLabel = d.ToString("dd.MM"),
                Values = counts
            });
        }

        var dailyAvg = new double[hourCount];
        for (var hi = 0; hi < hourCount; hi++)
        {
            var sum = queueByHourPerDay.Sum(s => s.Values[hi]);
            dailyAvg[hi] = periodDayCount > 0 ? sum / (double)periodDayCount : 0;
        }

        var heatmap = BuildHeatmap(
            completedStages,
            heatmapByDoctor,
            periodDayCount,
            doctorNameMap,
            cabinetLabelMap);

        return new ManagerAnalyticsViewModel
        {
            PeriodFrom = from,
            PeriodTo = to,
            FilterCabinetId = cabinetId,
            FilterDoctorId = doctorId,
            FilterCategoryId = categoryId,
            CabinetOptions = cabinetOpts,
            DoctorOptions = doctorOpts,
            CategoryOptions = catOpts,
            QueueByHourLabels = hourLabels,
            QueueByHourPerDay = queueByHourPerDay,
            QueueByHourDailyAverage = dailyAvg,
            WaitHistogram = waitHistogram,
            AvgWaitByDoctor = avgWaitByDoctor,
            AvgServiceByDoctor = avgServiceByDoctor,
            HeatmapHourLabels = hourLabels,
            HeatmapRowLabels = heatmap.RowLabels,
            HeatmapValues = heatmap.Values,
            HeatmapIsByDoctor = heatmapByDoctor
        };
    }

    private IReadOnlyList<string> BuildHourLabels()
    {
        var list = new List<string>();
        for (var h = _opt.WorkdayStartHour; h < _opt.WorkdayEndHour; h++)
            list.Add($"{h}:00");
        return list;
    }

    private sealed record CompletedStageRow(
        int IdDoctor,
        int IdCabinet,
        DateOnly DateArrival,
        double WaitMinutes,
        double ServiceMinutes,
        int TimeStartHour);

    private static List<CompletedStageRow> ExtractCompletedStages(
        List<EqAppointment> appointments,
        int? cabinetId,
        int? doctorId)
    {
        var list = new List<CompletedStageRow>();
        foreach (var a in appointments)
        {
            foreach (var li in a.ListItems)
            {
                if (li.TimeCall == null || li.TimeStartServicing == null || li.TimeEndServicing == null)
                    continue;
                if (cabinetId is int cab and > 0 && li.IdCabinet != cab)
                    continue;
                if (doctorId is int doc and > 0 && li.IdDoctor != doc)
                    continue;

                list.Add(new CompletedStageRow(
                    li.IdDoctor,
                    li.IdCabinet,
                    a.DateArrival,
                    WaitBeforeServiceMinutes(a.DateArrival, a.TimeArrival, li.TimeCall.Value),
                    ServiceMinutes(a.DateArrival, li.TimeStartServicing.Value, li.TimeEndServicing.Value),
                    li.TimeStartServicing.Value.Hour));
            }
        }

        return list;
    }

    private static int CountWaitingAt(
        List<EqAppointment> dayAppointments,
        DateTime slotEnd,
        int? cabinetId,
        int? doctorId,
        int? categoryId)
    {
        var count = 0;
        foreach (var a in dayAppointments)
        {
            if (categoryId is int cid and > 0 && a.IdCategory != cid)
                continue;

            var arrival = EqDateTimeExtensions.CombineOnArrivalDate(a.DateArrival, a.TimeArrival);
            if (arrival > slotEnd)
                continue;

            var left = a.TimeComplete != null &&
                EqDateTimeExtensions.CombineOnArrivalDate(a.DateArrival, a.TimeComplete.Value) <= slotEnd;
            if (left)
                continue;

            var ordered = a.ListItems.OrderBy(li => li.IdListItem).ToList();
            EqListItem? current = null;
            foreach (var li in ordered)
            {
                var stageDone = li.TimeEndServicing != null &&
                    EqDateTimeExtensions.CombineOnArrivalDate(a.DateArrival, li.TimeEndServicing.Value) <= slotEnd;
                if (!stageDone)
                {
                    current = li;
                    break;
                }
            }

            if (current == null)
                continue;

            if (cabinetId is int cab and > 0 && current.IdCabinet != cab)
                continue;
            if (doctorId is int doc and > 0 && current.IdDoctor != doc)
                continue;

            var called = current.TimeCall != null &&
                EqDateTimeExtensions.CombineOnArrivalDate(a.DateArrival, current.TimeCall.Value) <= slotEnd;
            if (called)
                continue;

            count++;
        }

        return count;
    }

    private static IReadOnlyList<HistogramBucketViewModel> BuildHistogram(IReadOnlyList<CompletedStageRow> rows)
    {
        var b0 = 0;
        var b1 = 0;
        var b2 = 0;
        var b3 = 0;
        var b4 = 0;
        foreach (var r in rows)
        {
            var w = r.WaitMinutes;
            if (w < 5) b0++;
            else if (w < 15) b1++;
            else if (w < 30) b2++;
            else if (w < 60) b3++;
            else b4++;
        }

        return new List<HistogramBucketViewModel>
        {
            new() { Label = "0–5 мин", Count = b0 },
            new() { Label = "5–15 мин", Count = b1 },
            new() { Label = "15–30 мин", Count = b2 },
            new() { Label = "30–60 мин", Count = b3 },
            new() { Label = "60+ мин", Count = b4 }
        };
    }

    private static IReadOnlyList<NamedValueViewModel> BuildDoctorAggregates(
        IReadOnlyList<CompletedStageRow> rows,
        Func<CompletedStageRow, double> selector,
        IReadOnlyDictionary<int, string> doctorNames,
        int topN)
    {
        var grouped = rows
            .Where(r => r.IdDoctor > 0)
            .GroupBy(r => r.IdDoctor)
            .Select(g => new NamedValueViewModel
            {
                Name = doctorNames.GetValueOrDefault(g.Key, $"Врач #{g.Key}"),
                ValueMinutes = g.Average(selector)
            })
            .OrderByDescending(x => x.ValueMinutes)
            .ToList();

        if (grouped.Count <= topN)
            return grouped;

        var top = grouped.Take(topN).ToList();
        var restAvg = grouped.Skip(topN).Average(x => x.ValueMinutes);
        top.Add(new NamedValueViewModel { Name = "Прочие", ValueMinutes = restAvg });
        return top;
    }

    private (IReadOnlyList<string> RowLabels, IReadOnlyList<IReadOnlyList<double>> Values) BuildHeatmap(
        IReadOnlyList<CompletedStageRow> rows,
        bool byDoctor,
        int periodDayCount,
        IReadOnlyDictionary<int, string> doctorNames,
        IReadOnlyDictionary<int, string> cabinetLabels)
    {
        var hours = Enumerable.Range(_opt.WorkdayStartHour, _opt.WorkdayEndHour - _opt.WorkdayStartHour).ToList();
        var dayDiv = Math.Max(1, periodDayCount);

        if (byDoctor)
        {
            var topIds = rows
                .Where(r => r.IdDoctor > 0)
                .GroupBy(r => r.IdDoctor)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(_opt.HeatmapTopN)
                .ToList();

            var matrix = new List<IReadOnlyList<double>>();
            var labels = new List<string>();
            foreach (var id in topIds)
            {
                labels.Add(doctorNames.GetValueOrDefault(id, $"#{id}"));
                var row = new double[hours.Count];
                for (var hi = 0; hi < hours.Count; hi++)
                {
                    var h = hours[hi];
                    var cnt = rows.Count(r => r.IdDoctor == id && r.TimeStartHour == h);
                    row[hi] = cnt / (double)dayDiv;
                }

                matrix.Add(row);
            }

            return (labels, matrix);
        }

        {
            var topIds = rows
                .GroupBy(r => r.IdCabinet)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(_opt.HeatmapTopN)
                .ToList();

            var matrix = new List<IReadOnlyList<double>>();
            var labels = new List<string>();
            foreach (var id in topIds)
            {
                labels.Add($"Каб. {cabinetLabels.GetValueOrDefault(id, id.ToString())}");
                var row = new double[hours.Count];
                for (var hi = 0; hi < hours.Count; hi++)
                {
                    var h = hours[hi];
                    var cnt = rows.Count(r => r.IdCabinet == id && r.TimeStartHour == h);
                    row[hi] = cnt / (double)dayDiv;
                }

                matrix.Add(row);
            }

            return (labels, matrix);
        }
    }

    private static double WaitBeforeServiceMinutes(DateOnly dateArrival, TimeOnly timeArrival, TimeOnly timeCall) =>
        (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeCall)
         - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeArrival)).TotalMinutes;

    private static double ServiceMinutes(DateOnly dateArrival, TimeOnly start, TimeOnly end) =>
        (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, end)
         - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, start)).TotalMinutes;
}
