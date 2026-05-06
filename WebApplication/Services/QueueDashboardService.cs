using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplication.Data;
using WebApplication.Models;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services;

/// <summary>
/// Мониторинг «сегодня» и текущая очередь. Порядок этапов маршрута — по возрастанию <see cref="EqListItem.IdListItem"/>.
/// </summary>
public sealed class QueueDashboardService : IQueueDashboardService
{
    private readonly ElectronicQueueDbContext _queue;
    private readonly MonitoringOptions _opt;

    public QueueDashboardService(ElectronicQueueDbContext queue, IOptions<MonitoringOptions> options)
    {
        _queue = queue;
        _opt = options.Value;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var todayDo = DateOnly.FromDateTime(today);
        var now = DateTime.UtcNow;

        var waitingCount = await (
            from li in _queue.ListItems
            join a in _queue.Appointments on li.IdAppointment equals a.IdAppointment
            where li.TimeCall == null
                  && li.TimeEndServicing == null
                  && a.TimeComplete == null
            select li
        ).CountAsync(cancellationToken).ConfigureAwait(false);

        var inServiceCount = await _queue.ListItems
            .CountAsync(li => li.TimeCall != null && li.TimeEndServicing == null, cancellationToken)
            .ConfigureAwait(false);

        var completedToday = await (
            from li in _queue.ListItems.AsNoTracking()
            join a in _queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            where a.DateArrival == todayDo
                  && li.TimeCall != null
                  && li.TimeStartServicing != null
                  && li.TimeEndServicing != null
            select new
            {
                a.DateArrival,
                a.TimeArrival,
                Call = li.TimeCall!.Value,
                Start = li.TimeStartServicing!.Value,
                End = li.TimeEndServicing!.Value
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var avgWait = completedToday.Count == 0
            ? 0
            : (int)Math.Round(completedToday.Average(x => WaitBeforeServiceMinutes(x.DateArrival, x.TimeArrival, x.Call)));
        var maxWait = completedToday.Count == 0
            ? 0
            : (int)Math.Round(completedToday.Max(x => WaitBeforeServiceMinutes(x.DateArrival, x.TimeArrival, x.Call)));
        var avgService = completedToday.Count == 0
            ? 0
            : (int)Math.Round(completedToday.Average(x => ServiceMinutes(x.DateArrival, x.Start, x.End)));
        var maxService = completedToday.Count == 0
            ? 0
            : (int)Math.Round(completedToday.Max(x => ServiceMinutes(x.DateArrival, x.Start, x.End)));

        var hourlyLabels = new List<string>();
        var hourlyWait = new List<int>();
        var hourlyService = new List<int>();
        for (var h = _opt.WorkdayStartHour; h < _opt.WorkdayEndHour; h++)
        {
            hourlyLabels.Add($"{h}:00");
            var bucket = completedToday.Where(x => x.Call.Hour == h).ToList();
            hourlyWait.Add(bucket.Count == 0
                ? 0
                : (int)Math.Round(bucket.Average(x => WaitBeforeServiceMinutes(x.DateArrival, x.TimeArrival, x.Call))));
            hourlyService.Add(bucket.Count == 0
                ? 0
                : (int)Math.Round(bucket.Average(x => ServiceMinutes(x.DateArrival, x.Start, x.End))));
        }

        var workdayMinutes = Math.Max(1, (_opt.WorkdayEndHour - _opt.WorkdayStartHour) * 60);

        var cabCompletedFixed = await GetLoadAggregatesTodayAsync(todayDo, byDoctor: false, cancellationToken).ConfigureAwait(false);
        var docCompletedFixed = await GetLoadAggregatesTodayAsync(todayDo, byDoctor: true, cancellationToken).ConfigureAwait(false);

        var cabinetsDb = await _queue.Cabinets.AsNoTracking().OrderBy(c => c.CabinetNumber).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var doctorsDb = await _queue.Doctors.AsNoTracking().OrderBy(d => d.FullName).ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var cabDict = cabCompletedFixed.ToDictionary(x => x.Id, x => x);
        var cabLabels = new List<string>();
        var cabDone = new List<int>();
        var cabBusy = new List<int>();
        foreach (var c in cabinetsDb)
        {
            var x = cabDict.GetValueOrDefault(c.IdCabinet);
            var sum = x?.ServiceSumMin ?? 0;
            var cnt = x?.Completed ?? 0;
            cabLabels.Add($"Каб. {c.CabinetNumber}");
            cabDone.Add(cnt);
            cabBusy.Add((int)Math.Min(100, Math.Round(sum * 100.0 / workdayMinutes)));
        }

        var docDict = docCompletedFixed.ToDictionary(x => x.Id, x => x);
        var docLabels = new List<string>();
        var docDone = new List<int>();
        var docBusy = new List<int>();
        foreach (var d in doctorsDb.Where(x => x.IdDoctor > 0))
        {
            var x = docDict.GetValueOrDefault(d.IdDoctor);
            var sum = x?.ServiceSumMin ?? 0;
            var cnt = x?.Completed ?? 0;
            docLabels.Add(d.FullName);
            docDone.Add(cnt);
            docBusy.Add((int)Math.Min(100, Math.Round(sum * 100.0 / workdayMinutes)));
        }

        var activeQueue = await BuildActiveQueueAsync(now, cancellationToken).ConfigureAwait(false);

        return new DashboardViewModel
        {
            WaitingCount = waitingCount,
            InServiceCount = inServiceCount,
            AvgWaitMinutes = avgWait,
            MaxWaitMinutes = maxWait,
            AvgServiceMinutes = avgService,
            MaxServiceMinutes = maxService,
            HourlyLabels = hourlyLabels,
            HourlyWaitMinutes = hourlyWait,
            HourlyServiceMinutes = hourlyService,
            CabinetLoadLabels = cabLabels,
            CabinetCompletedToday = cabDone,
            CabinetBusyPercent = cabBusy,
            DoctorLoadLabels = docLabels,
            DoctorCompletedToday = docDone,
            DoctorBusyPercent = docBusy,
            ActiveQueue = activeQueue
        };
    }

    private sealed record LoadAgg(int Id, int Completed, double ServiceSumMin);

    private async Task<List<LoadAgg>> GetLoadAggregatesTodayAsync(DateOnly todayDo, bool byDoctor, CancellationToken cancellationToken)
    {
        var raw = await (
            from li in _queue.ListItems.AsNoTracking()
            join a in _queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            where a.DateArrival == todayDo
                  && li.TimeCall != null && li.TimeStartServicing != null && li.TimeEndServicing != null
            select new
            {
                li.IdCabinet,
                li.IdDoctor,
                a.DateArrival,
                Start = li.TimeStartServicing!.Value,
                End = li.TimeEndServicing!.Value
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var groups = byDoctor
            ? raw.GroupBy(x => x.IdDoctor)
            : raw.GroupBy(x => x.IdCabinet);

        return groups.Select(g => new LoadAgg(
            g.Key,
            g.Count(),
            g.Sum(x => (EqDateTimeExtensions.CombineOnArrivalDate(x.DateArrival, x.End)
                        - EqDateTimeExtensions.CombineOnArrivalDate(x.DateArrival, x.Start)).TotalMinutes))).ToList();
    }

    private async Task<IReadOnlyList<DashboardQueueRowViewModel>> BuildActiveQueueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var open = await _queue.Appointments.AsNoTracking()
            .Where(a => a.TimeComplete == null)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Cabinet)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Doctor)
            .Include(a => a.Category)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<DashboardQueueRowViewModel>();
        foreach (var a in open)
        {
            var ordered = a.ListItems.OrderBy(li => li.IdListItem).ToList();
            var current = ordered.FirstOrDefault(li => li.TimeEndServicing == null);
            if (current == null)
                continue;

            var arrival = a.CombineArrival();
            int waitMin;
            if (current.TimeCall == null)
                waitMin = (int)Math.Max(0, Math.Round((nowUtc - arrival).TotalMinutes));
            else if (current.TimeStartServicing == null)
            {
                var callDt = EqDateTimeExtensions.CombineOnArrivalDate(a.DateArrival, current.TimeCall.Value);
                waitMin = (int)Math.Max(0, Math.Round((nowUtc - callDt).TotalMinutes));
            }
            else
                waitMin = 0;

            var cab = current.Cabinet != null ? $"Каб. {current.Cabinet.CabinetNumber}" : "—";
            var doc = current.Doctor != null && current.IdDoctor > 0 ? current.Doctor.FullName : "—";

            rows.Add(new DashboardQueueRowViewModel
            {
                IdAppointment = a.IdAppointment,
                Patient = string.IsNullOrWhiteSpace(a.Info) ? $"Талон #{a.IdAppointment}" : a.Info.Trim(),
                PriorityDisplay = $"Талон: {a.Priority}, кат.: {a.Category?.Priority ?? 0} ({a.Category?.Name ?? "—"})",
                TicketPriority = a.Priority,
                CategoryPriority = a.Category?.Priority ?? 0,
                WaitingMinutes = waitMin,
                CurrentCabinet = cab,
                CurrentDoctor = doc
            });
        }

        rows.Sort((x, y) =>
        {
            var c = y.TicketPriority.CompareTo(x.TicketPriority);
            if (c != 0) return c;
            c = y.CategoryPriority.CompareTo(x.CategoryPriority);
            return c != 0 ? c : y.WaitingMinutes.CompareTo(x.WaitingMinutes);
        });

        return rows;
    }

    private static double WaitBeforeServiceMinutes(DateOnly dateArrival, TimeOnly timeArrival, TimeOnly timeCall) =>
        (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeCall)
         - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeArrival)).TotalMinutes;

    private static double ServiceMinutes(DateOnly dateArrival, TimeOnly start, TimeOnly end) =>
        (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, end)
         - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, start)).TotalMinutes;
}
