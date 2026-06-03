using WebApplication.Models.ViewModels.Dashboard;

namespace WebApplication.Services.Dashboard;

public static class DashboardSnapshotMapper
{
    public static DashboardSnapshotDto ToSnapshot(DashboardViewModel model) =>
        new()
        {
            WaitingCount = model.WaitingCount,
            InServiceCount = model.InServiceCount,
            AcceptedTodayCount = model.AcceptedTodayCount,
            TicketsIssuedTodayCount = model.TicketsIssuedTodayCount,
            DoctorsOnShiftCount = model.DoctorsOnShiftCount,
            DoctorsTotalCount = model.DoctorsTotalCount,
            ActiveQueue = model.ActiveQueue.Select(ToQueueRow).ToList(),
            DoctorLoadCards = model.DoctorLoadCards.Select(ToDoctorCard).ToList(),
        };

    private static DashboardQueueRowDto ToQueueRow(DashboardQueueRowViewModel r) =>
        new()
        {
            IdAppointment = r.IdAppointment,
            TicketNumber = r.TicketNumber,
            TicketPriority = r.TicketPriority,
            CategoryPriority = r.CategoryPriority,
            WaitingMinutes = r.WaitingMinutes,
            CurrentCabinet = r.CurrentCabinet,
            CurrentDoctor = r.CurrentDoctor,
            Specialty = r.Specialty,
            IdSpecialty = r.IdSpecialty,
            IdStatusItem = r.IdStatusItem,
            StatusLabel = r.StatusLabel,
            StatusCode = r.StatusCode,
        };

    private static DoctorLoadCardDto ToDoctorCard(DoctorLoadCardViewModel d) =>
        new()
        {
            IdDoctor = d.IdDoctor,
            FullName = d.FullName,
            Specialty = d.Specialty,
            IdSpecialty = d.IdSpecialty,
            Cabinet = d.Cabinet,
            IsOnShift = d.IsOnShift,
            IsInService = d.IsInService,
            CurrentServiceMinutes = d.CurrentServiceMinutes,
            NormServiceMinutes = d.NormServiceMinutes,
            QueueLength = d.QueueLength,
        };
}
