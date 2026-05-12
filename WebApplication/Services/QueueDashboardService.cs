using System.Globalization;
using Microsoft.EntityFrameworkCore;
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

    public QueueDashboardService(ElectronicQueueDbContext queue) => _queue = queue;

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var todayDo = DateOnly.FromDateTime(DateTime.UtcNow.Date);
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

        var acceptedTodayCount = completedToday.Count;

        var statusRows = await _queue.StatusItemLists.AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var noShowStatusIds = statusRows
            .Where(s => QueueDashboardStatusMapper.IsNoShowStatusName(s.Name))
            .Select(s => s.IdStatusItem)
            .ToHashSet();

        var noShowTodayCount = noShowStatusIds.Count == 0
            ? 0
            : await (
                from a in _queue.Appointments.AsNoTracking()
                join li in _queue.ListItems.AsNoTracking() on a.IdAppointment equals li.IdAppointment
                where a.DateArrival == todayDo && noShowStatusIds.Contains(li.IdStatusItem)
                select a.IdAppointment
            ).Distinct().CountAsync(cancellationToken).ConfigureAwait(false);

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

        var doctorsDb = await _queue.Doctors.AsNoTracking().OrderBy(d => d.FullName).ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var open = await LoadOpenAppointmentsAsync(cancellationToken).ConfigureAwait(false);
        var activeQueue = BuildActiveQueueFromOpen(open, now);
        var doctorLoadCards = BuildDoctorLoadCards(open, now, doctorsDb);

        return new DashboardViewModel
        {
            WaitingCount = waitingCount,
            InServiceCount = inServiceCount,
            AcceptedTodayCount = acceptedTodayCount,
            NoShowTodayCount = noShowTodayCount,
            AvgWaitMinutes = avgWait,
            MaxWaitMinutes = maxWait,
            AvgServiceMinutes = avgService,
            MaxServiceMinutes = maxService,
            ActiveQueue = activeQueue,
            DoctorLoadCards = doctorLoadCards
        };
    }

    private async Task<List<EqAppointment>> LoadOpenAppointmentsAsync(CancellationToken cancellationToken) =>
        await _queue.Appointments.AsNoTracking()
            .Where(a => a.TimeComplete == null)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Cabinet)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Doctor)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Specialty)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.StatusItem)
            .Include(a => a.Category)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static IReadOnlyList<DashboardQueueRowViewModel> BuildActiveQueueFromOpen(
        IReadOnlyList<EqAppointment> open,
        DateTime nowUtc)
    {
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
            var spec = string.IsNullOrWhiteSpace(current.Specialty?.Definition)
                ? "—"
                : current.Specialty!.Definition.Trim();
            var (statusLabel, statusCode) = QueueDashboardStatusMapper.ResolveForCurrentStep(current);

            rows.Add(new DashboardQueueRowViewModel
            {
                IdAppointment = a.IdAppointment,
                Patient = string.IsNullOrWhiteSpace(a.Info) ? $"Запись №{a.IdAppointment}" : a.Info.Trim(),
                TicketPriority = a.Priority,
                CategoryPriority = a.Category?.Priority ?? 0,
                WaitingMinutes = waitMin,
                CurrentCabinet = cab,
                CurrentDoctor = doc,
                Specialty = spec,
                ArrivalTime = a.TimeArrival.ToString("HH:mm", CultureInfo.InvariantCulture),
                StatusLabel = statusLabel,
                StatusCode = statusCode
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

    private static IReadOnlyList<DoctorLoadCardViewModel> BuildDoctorLoadCards(
        IReadOnlyList<EqAppointment> open,
        DateTime nowUtc,
        IReadOnlyList<EqDoctor> doctorsOrdered)
    {
        var cards = new List<DoctorLoadCardViewModel>();
        foreach (var doc in doctorsOrdered.Where(d => d.IdDoctor > 0))
        {
            EqListItem? inServiceLi = null;
            EqAppointment? inServiceAppt = null;
            foreach (var a in open)
            {
                foreach (var li in a.ListItems)
                {
                    if (li.IdDoctor != doc.IdDoctor)
                        continue;
                    if (li.TimeStartServicing.HasValue && !li.TimeEndServicing.HasValue)
                    {
                        if (inServiceLi == null || li.IdListItem < inServiceLi.IdListItem)
                        {
                            inServiceLi = li;
                            inServiceAppt = a;
                        }
                    }
                }
            }

            var queueLen = 0;
            foreach (var a in open)
            {
                foreach (var li in a.ListItems)
                {
                    if (li.IdDoctor != doc.IdDoctor || li.TimeEndServicing.HasValue)
                        continue;
                    if (inServiceLi != null && li.IdListItem == inServiceLi.IdListItem)
                        continue;
                    var (_, code) = QueueDashboardStatusMapper.ResolveForCurrentStep(li);
                    if (QueueDashboardStatusMapper.IsWaitingOrCalledCode(code))
                        queueLen++;
                }
            }

            var isInService = inServiceLi != null;
            if (!isInService && queueLen == 0)
                continue;

            int? currentMin = null;
            int? normMin = null;
            if (inServiceLi != null && inServiceAppt != null)
            {
                var startDt = EqDateTimeExtensions.CombineOnArrivalDate(
                    inServiceAppt.DateArrival,
                    inServiceLi.TimeStartServicing!.Value);
                currentMin = (int)Math.Max(0, Math.Round((nowUtc - startDt).TotalMinutes));
                var n = inServiceLi.Specialty?.TimeServicing ?? 0;
                normMin = n > 0 ? n : null;
            }

            var specialtyDisplay = "—";
            if (inServiceLi?.Specialty?.Definition is { } def1 && !string.IsNullOrWhiteSpace(def1))
                specialtyDisplay = def1.Trim();
            else
            {
                foreach (var a in open)
                {
                    foreach (var li in a.ListItems.OrderBy(x => x.IdListItem))
                    {
                        if (li.IdDoctor != doc.IdDoctor || li.TimeEndServicing.HasValue)
                            continue;
                        if (inServiceLi != null && li.IdListItem == inServiceLi.IdListItem)
                            continue;
                        var (_, code) = QueueDashboardStatusMapper.ResolveForCurrentStep(li);
                        if (!QueueDashboardStatusMapper.IsWaitingOrCalledCode(code))
                            continue;
                        if (!string.IsNullOrWhiteSpace(li.Specialty?.Definition))
                        {
                            specialtyDisplay = li.Specialty!.Definition.Trim();
                            break;
                        }
                    }

                    if (specialtyDisplay != "—")
                        break;
                }
            }

            var cabinet = inServiceLi?.Cabinet?.CabinetNumber is { } cabNum
                ? $"Каб. {cabNum}"
                : "";

            cards.Add(new DoctorLoadCardViewModel
            {
                IdDoctor = doc.IdDoctor,
                FullName = doc.FullName,
                Specialty = specialtyDisplay,
                Cabinet = cabinet,
                IsInService = isInService,
                CurrentServiceMinutes = currentMin,
                NormServiceMinutes = normMin,
                QueueLength = queueLen
            });
        }

        static int OverDelta(DoctorLoadCardViewModel x) =>
            x.IsInService && x.NormServiceMinutes is int n && x.CurrentServiceMinutes is int c && c > n
                ? c - n
                : int.MinValue;

        static int Group(DoctorLoadCardViewModel x) =>
            OverDelta(x) > int.MinValue ? 0 : x.IsInService ? 1 : 2;

        cards.Sort((x, y) =>
        {
            var g = Group(x).CompareTo(Group(y));
            if (g != 0) return g;
            if (Group(x) == 0) return OverDelta(y).CompareTo(OverDelta(x));
            return string.Compare(x.FullName, y.FullName, StringComparison.Ordinal);
        });

        return cards;
    }

    private static double WaitBeforeServiceMinutes(DateOnly dateArrival, TimeOnly timeArrival, TimeOnly timeCall) =>
        (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeCall)
         - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeArrival)).TotalMinutes;

    private static double ServiceMinutes(DateOnly dateArrival, TimeOnly start, TimeOnly end) =>
        (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, end)
         - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, start)).TotalMinutes;
}
