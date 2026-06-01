using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Dashboard;

public static class QueueDashboardDoctorQueueCount
{
    
    public static int CountForDoctor(
        int idDoctor,
        IReadOnlyList<EqAppointment> openAppointments,
        EqListItem? excludeInServiceListItem = null)
    {
        var count = 0;
        foreach (var appointment in openAppointments)
        {
            var current = appointment.ListItems
                .OrderBy(li => li.IdListItem)
                .FirstOrDefault(li => li.TimeEndServicing == null);
            if (current == null
                || current.IdDoctor != idDoctor
                || QueueDashboardStatusMapper.IsExcludedStatusItem(current))
                continue;

            if (excludeInServiceListItem != null && current.IdListItem == excludeInServiceListItem.IdListItem)
                continue;

            var (_, code) = QueueDashboardStatusMapper.ResolveForCurrentStep(current);
            if (QueueDashboardStatusMapper.IsWaitingOrCalledCode(code))
                count++;
        }

        return count;
    }
}
