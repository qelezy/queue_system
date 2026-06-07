using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Models.ViewModels.Dashboard;
namespace WebApplication.Services.Dashboard;

public sealed class QueueDashboardService : IQueueDashboardService
{
    private readonly ElectronicQueueDbContext _queue;
    private readonly IQueueDashboardClock _clock;

    public QueueDashboardService(ElectronicQueueDbContext queue, IQueueDashboardClock clock)
    {
        _queue = queue;
        _clock = clock;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var todayDo = _clock.TodayDateOnly();
        var now = _clock.Now();

        var acceptedTodayCount = await CountServicedPatientsTodayAsync(todayDo, cancellationToken)
            .ConfigureAwait(false);

        var ticketsIssuedTodayCount = await CountTicketsIssuedTodayAsync(todayDo, cancellationToken)
            .ConfigureAwait(false);

        var openShiftsByDoctor = await QueueDashboardDoctorsOnShiftQuery.LoadOpenShiftsByDoctorAsync(
                _queue.LogWorks.AsNoTracking(),
                todayDo,
                cancellationToken)
            .ConfigureAwait(false);

        var doctorsTotalCount = await QueueDashboardDoctorsOnShiftQuery.CountTotalDoctorsAsync(
                _queue.Doctors.AsNoTracking(),
                cancellationToken)
            .ConfigureAwait(false);

        var doctorsDb = await _queue.Doctors.AsNoTracking().OrderBy(d => d.FullName).ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var queueFilters = await LoadQueueFiltersAsync(cancellationToken).ConfigureAwait(false);

        var open = await LoadOpenAppointmentsAsync(todayDo, cancellationToken).ConfigureAwait(false);
        var activeQueue = BuildActiveQueueFromOpen(open, now);
        var waitingCount = CountWaitingFromOpen(open);
        var doctorLoadCards = BuildDoctorLoadCards(open, now, doctorsDb, openShiftsByDoctor);
        var inServiceCount = doctorLoadCards.Count(c => c.IsInService);
        var doctorsOnShiftCount = doctorLoadCards.Count;

        return new DashboardViewModel
        {
            WaitingCount = waitingCount,
            InServiceCount = inServiceCount,
            AcceptedTodayCount = acceptedTodayCount,
            TicketsIssuedTodayCount = ticketsIssuedTodayCount,
            ActiveQueue = activeQueue,
            QueueFilters = queueFilters,
            DoctorLoadCards = doctorLoadCards,
            DoctorsOnShiftCount = doctorsOnShiftCount,
            DoctorsTotalCount = doctorsTotalCount
        };
    }

    public async Task<AppointmentCompletedStagesResponse?> GetRouteStagesAsync(
        int idAppointment,
        CancellationToken cancellationToken = default)
    {
        var todayDo = _clock.TodayDateOnly();

        var appointment = await _queue.Appointments.AsNoTracking()
            .Where(a => a.IdAppointment == idAppointment && a.DateArrival == todayDo)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Specialty)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Cabinet)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.StatusItem)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (appointment == null)
            return null;

        var stages = appointment.ListItems
            .Where(QueueDashboardCompletedStagesMapper.IsRouteStage)
            .OrderBy(li => li.IdListItem)
            .Select(QueueDashboardCompletedStagesMapper.ToDto)
            .ToList();

        var ticketNumber = string.IsNullOrWhiteSpace(appointment.Number)
            ? "—"
            : appointment.Number.Trim();

        return new AppointmentCompletedStagesResponse
        {
            TicketNumber = ticketNumber,
            Stages = stages,
        };
    }

    private async Task<List<EqAppointment>> LoadOpenAppointmentsAsync(
        DateOnly todayDo,
        CancellationToken cancellationToken) =>
        await _queue.Appointments.AsNoTracking()
            .Where(a => a.TimeComplete == null && a.DateArrival == todayDo)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Cabinet)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Doctor)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.Specialty)
            .Include(a => a.ListItems)
            .ThenInclude(li => li.StatusItem)
            .Include(a => a.Category)
            .Include(a => a.StatusAppointment)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<int> CountTicketsIssuedTodayAsync(
        DateOnly todayDo,
        CancellationToken cancellationToken) =>
        await _queue.Appointments.AsNoTracking()
            .CountAsync(a => a.DateArrival == todayDo, cancellationToken)
            .ConfigureAwait(false);

    private async Task<int> CountServicedPatientsTodayAsync(
        DateOnly todayDo,
        CancellationToken cancellationToken)
    {
        var withTimeComplete = await _queue.Appointments.AsNoTracking()
            .CountAsync(a => a.DateArrival == todayDo && a.TimeComplete != null, cancellationToken)
            .ConfigureAwait(false);

        var routeClosedWithoutTicketComplete = await _queue.Appointments.AsNoTracking()
            .CountAsync(
                a => a.DateArrival == todayDo
                     && a.TimeComplete == null
                     && a.ListItems.Any()
                     && !a.ListItems.Any(li => li.TimeEndServicing == null),
                cancellationToken)
            .ConfigureAwait(false);

        return withTimeComplete + routeClosedWithoutTicketComplete;
    }

    private async Task<DashboardQueueFilterViewModel> LoadQueueFiltersAsync(CancellationToken cancellationToken)
    {
        var categories = await _queue.Categories.AsNoTracking()
            .Where(c => !c.Old)
            .OrderBy(c => c.Name)
            .Select(c => new DashboardFilterOption(c.IdCategory, c.Name.Trim()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var specialties = await _queue.Specialties.AsNoTracking()
            .OrderBy(s => s.Definition)
            .Select(s => new DashboardFilterOption(s.IdSpecialty, s.Definition.Trim()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DashboardQueueFilterViewModel
        {
            Categories = categories,
            Specialties = specialties,
            Statuses = []
        };
    }

    private static IReadOnlyList<DashboardQueueRowViewModel> BuildActiveQueueFromOpen(
        IReadOnlyList<EqAppointment> open,
        DateTime nowUtc)
    {
        var rows = new List<DashboardQueueRowViewModel>();
        foreach (var a in open)
        {
            var ordered = a.ListItems.OrderBy(li => li.IdListItem).ToList();
            var current = ordered.FirstOrDefault(li => li.TimeEndServicing == null);
            if (current == null || !QueueDashboardStatusMapper.IsWaitingListStep(current))
                continue;

            var waitFrom = current.TimeCall is { } timeCall
                ? EqDateTimeExtensions.CombineOnArrivalDate(a.DateArrival, timeCall)
                : a.CombineArrival();
            var waitMin = WaitMinutesFrom(waitFrom, nowUtc);
            var neededSpecialtiesCount = ordered.Count(li => li.TimeEndServicing == null);
            var completedSpecialtiesCount = ordered.Count(li => li.TimeEndServicing != null);
            var (statusLabel, statusCode) = QueueDashboardStatusMapper.ResolveForCurrentStep(current);

            rows.Add(new DashboardQueueRowViewModel
            {
                IdAppointment = a.IdAppointment,
                TicketNumber = string.IsNullOrWhiteSpace(a.Number) ? "—" : a.Number.Trim(),
                TicketPriority = a.Priority,
                CategoryPriority = a.Category?.Priority ?? 0,
                CategoryName = string.IsNullOrWhiteSpace(a.Category?.Name) ? "—" : a.Category!.Name.Trim(),
                WaitingMinutes = waitMin,
                NeededSpecialtiesCount = neededSpecialtiesCount,
                CompletedSpecialtiesCount = completedSpecialtiesCount,
                IdCategory = a.IdCategory,
                IdSpecialty = current.IdSpecialty,
                IdStatusItem = current.IdStatusItem,
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

    private static int CountWaitingFromOpen(IReadOnlyList<EqAppointment> open)
    {
        var count = 0;
        foreach (var a in open)
        {
            var current = a.ListItems.OrderBy(li => li.IdListItem)
                .FirstOrDefault(li => li.TimeEndServicing == null);
            if (current != null && QueueDashboardStatusMapper.IsWaitingListStep(current))
                count++;
        }

        return count;
    }

    private static IReadOnlyList<DoctorLoadCardViewModel> BuildDoctorLoadCards(
        IReadOnlyList<EqAppointment> open,
        DateTime nowUtc,
        IReadOnlyList<EqDoctor> doctorsOrdered,
        IReadOnlyDictionary<int, DoctorOpenShiftEntry> openShiftsByDoctor)
    {
        var cards = new List<DoctorLoadCardViewModel>();
        foreach (var doc in doctorsOrdered.Where(d => d.IdDoctor > 0))
        {
            EqListItem? inServiceLi = null;
            EqAppointment? inServiceAppt = null;
            foreach (var a in open)
            {
                var current = a.ListItems.OrderBy(li => li.IdListItem)
                    .FirstOrDefault(li => li.TimeEndServicing == null);
                if (current == null || current.IdDoctor != doc.IdDoctor)
                    continue;
                if (!QueueDashboardStatusMapper.IsInServiceStep(current))
                    continue;
                if (inServiceLi == null || current.IdListItem < inServiceLi.IdListItem)
                {
                    inServiceLi = current;
                    inServiceAppt = a;
                }
            }

            var queueLen = QueueDashboardDoctorQueueCount.CountForDoctor(
                doc.IdDoctor,
                open,
                inServiceLi);

            var isInService = inServiceLi != null;
            var isOnShift = openShiftsByDoctor.ContainsKey(doc.IdDoctor);
            if (!isInService && queueLen == 0 && !isOnShift)
                continue;

            int? currentMin = null;
            int? normMin = null;
            if (inServiceLi != null && inServiceAppt != null)
            {
                var startDt = EqDateTimeExtensions.CombineOnArrivalDate(
                    inServiceAppt.DateArrival,
                    inServiceLi.TimeStartServicing!.Value);
                currentMin = QueueDashboardElapsedMinutes.ElapsedWholeMinutes(startDt, nowUtc);
                var n = inServiceLi.Specialty?.TimeServicing ?? 0;
                normMin = n > 0 ? n : null;
            }

            var specialtyDisplay = "—";
            var idSpecialty = 0;
            if (inServiceLi?.Specialty?.Definition is { } def1 && !string.IsNullOrWhiteSpace(def1))
            {
                specialtyDisplay = def1.Trim();
                idSpecialty = inServiceLi.IdSpecialty;
            }
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
                            idSpecialty = li.IdSpecialty;
                            break;
                        }
                    }

                    if (specialtyDisplay != "—")
                        break;
                }
            }

            var cabinet = inServiceLi?.Cabinet?.CabinetNumber is { } cabNum && !string.IsNullOrWhiteSpace(cabNum)
                ? cabNum.Trim()
                : isOnShift && openShiftsByDoctor.TryGetValue(doc.IdDoctor, out var shift)
                    ? shift.CabinetNumber
                    : "";

            var currentTicketNumber = isInService && inServiceAppt != null
                ? string.IsNullOrWhiteSpace(inServiceAppt.Number) ? null : inServiceAppt.Number.Trim()
                : null;

            cards.Add(new DoctorLoadCardViewModel
            {
                IdDoctor = doc.IdDoctor,
                FullName = doc.FullName,
                Specialty = specialtyDisplay,
                IdSpecialty = idSpecialty,
                Cabinet = cabinet,
                IsOnShift = isOnShift,
                IsInService = isInService,
                CurrentTicketNumber = currentTicketNumber,
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

    public async Task<DoctorPotentialPatientsResponse?> GetDoctorPotentialPatientsAsync(
        int idDoctor,
        CancellationToken cancellationToken = default)
    {
        var todayDo = _clock.TodayDateOnly();
        var now = _clock.Now();

        var doctor = await _queue.Doctors.AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdDoctor == idDoctor, cancellationToken)
            .ConfigureAwait(false);

        if (doctor == null)
            return null;

        var open = await LoadOpenAppointmentsAsync(todayDo, cancellationToken).ConfigureAwait(false);
        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(idDoctor, open, now);

        return new DoctorPotentialPatientsResponse
        {
            DoctorName = doctor.FullName,
            Patients = patients
        };
    }

    private static int WaitMinutesFrom(DateTime fromUtc, DateTime toUtc) =>
        QueueDashboardElapsedMinutes.ElapsedWholeMinutes(fromUtc, toUtc);
}
