using Microsoft.Extensions.Options;
using WebApplication.Models;

namespace WebApplication.Services;

public sealed class MockQueueDashboardService : IQueueDashboardService
{
    private readonly MonitoringOptions _opt;

    public MockQueueDashboardService(IOptions<MonitoringOptions> options) =>
        _opt = options.Value;

    public Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var hourlyLabels = ElectronicQueueMockData.BuildHourLabels(_opt).ToList();
        var n = hourlyLabels.Count;
        var hourlyWait = Enumerable.Range(0, n).Select(i => 5 + (i * 3) % 25).ToList();
        var hourlyService = Enumerable.Range(0, n).Select(i => 8 + (i * 2) % 18).ToList();

        var cabs = ElectronicQueueMockData.Cabinets;
        var docs = ElectronicQueueMockData.Doctors;

        var vm = new DashboardViewModel
        {
            WaitingCount = 12,
            InServiceCount = 5,
            AvgWaitMinutes = 18,
            MaxWaitMinutes = 42,
            AvgServiceMinutes = 22,
            MaxServiceMinutes = 55,
            HourlyLabels = hourlyLabels,
            HourlyWaitMinutes = hourlyWait,
            HourlyServiceMinutes = hourlyService,
            CabinetLoadLabels = cabs.Select(c => c.Label).ToList(),
            CabinetCompletedToday = new List<int> { 14, 11, 9 },
            CabinetBusyPercent = new List<int> { 45, 38, 33 },
            DoctorLoadLabels = docs.Select(d => d.Name).ToList(),
            DoctorCompletedToday = new List<int> { 12, 10, 8, 7 },
            DoctorBusyPercent = new List<int> { 52, 41, 36, 30 },
            ActiveQueue =
            [
                new DashboardQueueRowViewModel
                {
                    IdAppointment = 1001,
                    Patient = "Демо: талон #1001",
                    PriorityDisplay = "Талон: 2, кат.: 1 (ОМС)",
                    TicketPriority = 2,
                    CategoryPriority = 1,
                    WaitingMinutes = 24,
                    CurrentCabinet = "Каб. 101",
                    CurrentDoctor = docs[0].Name
                },
                new DashboardQueueRowViewModel
                {
                    IdAppointment = 1002,
                    Patient = "Демо: талон #1002",
                    PriorityDisplay = "Талон: 1, кат.: 2 (Платно)",
                    TicketPriority = 1,
                    CategoryPriority = 2,
                    WaitingMinutes = 8,
                    CurrentCabinet = "Каб. 102",
                    CurrentDoctor = docs[1].Name
                }
            ]
        };

        return Task.FromResult(vm);
    }
}
