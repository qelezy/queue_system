using WebApplication.Models.ViewModels.Dashboard;

namespace WebApplication.Services.Dashboard;

public static class DashboardSnapshotMapper
{
    public static DashboardSnapshotDto ToSnapshot(DashboardViewModel model, bool isDemoData) =>
        new()
        {
            IsDemoData = isDemoData,
            WaitingCount = model.WaitingCount,
            InServiceCount = model.InServiceCount,
            AcceptedTodayCount = model.AcceptedTodayCount,
            AvgWaitMinutes = model.AvgWaitMinutes,
            MaxWaitMinutes = model.MaxWaitMinutes,
            AvgServiceMinutes = model.AvgServiceMinutes,
            MaxServiceMinutes = model.MaxServiceMinutes,
            ActiveQueue = model.ActiveQueue.Select(ToQueueRow).ToList(),
            DoctorLoadCards = model.DoctorLoadCards.Select(ToDoctorCard).ToList(),
        };

    private static DashboardQueueRowDto ToQueueRow(DashboardQueueRowViewModel r) =>
        new()
        {
            IdAppointment = r.IdAppointment,
            Patient = r.Patient,
            TicketPriority = r.TicketPriority,
            CategoryPriority = r.CategoryPriority,
            WaitingMinutes = r.WaitingMinutes,
            CurrentCabinet = r.CurrentCabinet,
            CurrentDoctor = r.CurrentDoctor,
            Specialty = r.Specialty,
            ArrivalTime = r.ArrivalTime,
            StatusLabel = r.StatusLabel,
            StatusCode = r.StatusCode,
        };

    private static DoctorLoadCardDto ToDoctorCard(DoctorLoadCardViewModel d) =>
        new()
        {
            IdDoctor = d.IdDoctor,
            FullName = d.FullName,
            Specialty = d.Specialty,
            Cabinet = d.Cabinet,
            IsInService = d.IsInService,
            CurrentServiceMinutes = d.CurrentServiceMinutes,
            NormServiceMinutes = d.NormServiceMinutes,
            QueueLength = d.QueueLength,
        };
}
