using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Dashboard;

public static class QueueDashboardDoctorPotentialPatients
{
    public static IReadOnlyList<DoctorPotentialPatientDto> BuildForDoctor(
        int idDoctor,
        IReadOnlyList<EqAppointment> openAppointments,
        DateTime nowUtc)
    {
        var rows = new List<(DoctorPotentialPatientDto Dto, int WaitingMinutes)>();

        foreach (var a in openAppointments)
        {
            var ordered = a.ListItems.OrderBy(li => li.IdListItem).ToList();
            var current = ordered.FirstOrDefault(li => li.TimeEndServicing == null);
            if (current == null || current.IdDoctor != idDoctor)
                continue;

            var (_, statusCode) = QueueDashboardStatusMapper.ResolveForCurrentStep(current);
            if (!QueueDashboardStatusMapper.IsWaitingOrCalledCode(statusCode))
                continue;

            var waitFrom = ResolveWaitFrom(a, current, ordered);
            var waitMin = QueueDashboardElapsedMinutes.ElapsedWholeMinutes(waitFrom, nowUtc);
            var categoryPriority = a.Category?.Priority ?? 0;

            rows.Add((
                new DoctorPotentialPatientDto
                {
                    TicketNumber = string.IsNullOrWhiteSpace(a.Number) ? "—" : a.Number.Trim(),
                    CategoryName = string.IsNullOrWhiteSpace(a.Category?.Name) ? "—" : a.Category!.Name.Trim(),
                    Priority = categoryPriority,
                    WaitingMinutes = waitMin,
                },
                waitMin));
        }

        rows.Sort((x, y) =>
        {
            var c = y.Dto.Priority.CompareTo(x.Dto.Priority);
            return c != 0 ? c : y.WaitingMinutes.CompareTo(x.WaitingMinutes);
        });

        return rows.Select(r => r.Dto).ToList();
    }

    private static DateTime ResolveWaitFrom(
        EqAppointment appointment,
        EqListItem current,
        IReadOnlyList<EqListItem> ordered)
    {
        if (current.TimeCall is { } timeCall)
            return EqDateTimeExtensions.CombineOnArrivalDate(appointment.DateArrival, timeCall);

        var previous = ordered
            .Where(li => li.IdListItem < current.IdListItem && li.TimeEndServicing.HasValue)
            .LastOrDefault();

        if (previous?.TimeEndServicing is { } previousEnd)
            return EqDateTimeExtensions.CombineOnArrivalDate(appointment.DateArrival, previousEnd);

        return appointment.CombineArrival();
    }
}
