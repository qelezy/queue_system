
namespace WebApplication.Services.Dashboard;

public sealed class MockQueueDashboardService : IQueueDashboardService
{
    public Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var docs = ElectronicQueueMockData.Doctors;

        var vm = new DashboardViewModel
        {
            WaitingCount = 12,
            InServiceCount = 5,
            AcceptedTodayCount = 38,
            NoShowTodayCount = 4,
            AvgWaitMinutes = 18,
            MaxWaitMinutes = 42,
            AvgServiceMinutes = 22,
            MaxServiceMinutes = 55,
            ActiveQueue =
            [
                new DashboardQueueRowViewModel
                {
                    IdAppointment = 1001,
                    Patient = "Иванов П. С.",
                    TicketPriority = 2,
                    CategoryPriority = 1,
                    WaitingMinutes = 24,
                    CurrentCabinet = "Каб. 101",
                    CurrentDoctor = docs[0].Name,
                    Specialty = "Терапевт",
                    ArrivalTime = "09:15",
                    StatusLabel = "Ожидает",
                    StatusCode = "waiting"
                },
                new DashboardQueueRowViewModel
                {
                    IdAppointment = 1002,
                    Patient = "Петрова А. В.",
                    TicketPriority = 1,
                    CategoryPriority = 2,
                    WaitingMinutes = 8,
                    CurrentCabinet = "Каб. 102",
                    CurrentDoctor = docs[1].Name,
                    Specialty = "Невролог",
                    ArrivalTime = "09:42",
                    StatusLabel = "Вызван",
                    StatusCode = "called"
                },
                new DashboardQueueRowViewModel
                {
                    IdAppointment = 1003,
                    Patient = "Сидоров К. М.",
                    TicketPriority = 1,
                    CategoryPriority = 1,
                    WaitingMinutes = 0,
                    CurrentCabinet = "Каб. 103",
                    CurrentDoctor = docs[2].Name,
                    Specialty = "Хирург",
                    ArrivalTime = "10:05",
                    StatusLabel = "На приёме",
                    StatusCode = "in-service"
                }
            ],
            DoctorLoadCards =
            [
                new DoctorLoadCardViewModel
                {
                    IdDoctor = 3,
                    FullName = docs[2].Name,
                    Specialty = "Хирург",
                    Cabinet = "Каб. 103",
                    IsInService = true,
                    CurrentServiceMinutes = 28,
                    NormServiceMinutes = 15,
                    QueueLength = 0
                },
                new DoctorLoadCardViewModel
                {
                    IdDoctor = 1,
                    FullName = docs[0].Name,
                    Specialty = "Терапевт",
                    Cabinet = "Каб. 101",
                    IsInService = true,
                    CurrentServiceMinutes = 15,
                    NormServiceMinutes = 20,
                    QueueLength = 2
                },
                new DoctorLoadCardViewModel
                {
                    IdDoctor = 2,
                    FullName = docs[1].Name,
                    Specialty = "Невролог",
                    Cabinet = "",
                    IsInService = false,
                    CurrentServiceMinutes = null,
                    NormServiceMinutes = null,
                    QueueLength = 1
                }
            ]
        };

        return Task.FromResult(vm);
    }
}
