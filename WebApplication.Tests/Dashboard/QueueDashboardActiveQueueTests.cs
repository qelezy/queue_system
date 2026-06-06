using System.Reflection;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Models.ViewModels.Dashboard;
using WebApplication.Services.Dashboard;
using Xunit;

namespace WebApplication.Tests.Dashboard;

public sealed class QueueDashboardActiveQueueTests
{
    private static readonly DateTime Now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildActiveQueue_counts_remaining_and_completed_specialties()
    {
        var appointment = new EqAppointment
        {
            IdAppointment = 1,
            Number = "A-1",
            Priority = 1,
            IdCategory = 7,
            DateArrival = new DateOnly(2026, 6, 5),
            TimeArrival = new TimeOnly(11, 45),
            Category = new EqCategory { IdCategory = 7, Name = "ОМС", Priority = 1 },
            ListItems =
            [
                CompletedStep(1),
                WaitingStep(2),
                FutureStep(3)
            ]
        };

        var rows = BuildActiveQueue([appointment], Now);

        var row = Assert.Single(rows);
        Assert.Equal("ОМС", row.CategoryName);
        Assert.Equal(7, row.IdCategory);
        Assert.Equal(2, row.NeededSpecialtiesCount);
        Assert.Equal(1, row.CompletedSpecialtiesCount);
    }

    private static IReadOnlyList<DashboardQueueRowViewModel> BuildActiveQueue(
        IReadOnlyList<EqAppointment> open,
        DateTime nowUtc)
    {
        var method = typeof(QueueDashboardService).GetMethod(
            "BuildActiveQueueFromOpen",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IReadOnlyList<DashboardQueueRowViewModel>>(
            method.Invoke(null, [open, nowUtc]));
    }

    private static EqListItem CompletedStep(int id) =>
        new()
        {
            IdListItem = id,
            IdSpecialty = id,
            TimeEndServicing = new TimeOnly(11, 0),
            StatusItem = new EqStatusItemList { Name = "Завершён" }
        };

    private static EqListItem WaitingStep(int id) =>
        new()
        {
            IdListItem = id,
            IdSpecialty = id,
            StatusItem = new EqStatusItemList { Name = "Ожидает" }
        };

    private static EqListItem FutureStep(int id) =>
        new()
        {
            IdListItem = id,
            IdSpecialty = id,
            StatusItem = new EqStatusItemList { Name = "Ожидает" }
        };
}
