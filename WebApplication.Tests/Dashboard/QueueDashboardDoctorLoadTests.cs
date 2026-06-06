using System.Reflection;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Models.ViewModels.Dashboard;
using WebApplication.Services.Dashboard;
using Xunit;

namespace WebApplication.Tests.Dashboard;

public sealed class QueueDashboardDoctorLoadTests
{
    private const int DoctorId = 42;
    private static readonly DateTime Now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildDoctorLoadCards_in_service_sets_current_ticket_number()
    {
        var inService = InServiceStep(1, DoctorId);
        var appointment = new EqAppointment
        {
            IdAppointment = 1,
            Number = "P7428",
            DateArrival = new DateOnly(2026, 6, 5),
            TimeArrival = new TimeOnly(9, 0),
            ListItems = [inService]
        };
        inService.IdAppointment = 1;
        inService.Specialty = new EqSpecialty { Definition = "Терапевт", TimeServicing = 5 };

        var cards = BuildDoctorLoadCards(
            [appointment],
            [new EqDoctor { IdDoctor = DoctorId, FullName = "Иванов И.И." }],
            new Dictionary<int, DoctorOpenShiftEntry>());

        var card = Assert.Single(cards);
        Assert.True(card.IsInService);
        Assert.Equal("P7428", card.CurrentTicketNumber);
    }

    [Fact]
    public void BuildDoctorLoadCards_waiting_for_patient_has_no_current_ticket_number()
    {
        var cards = BuildDoctorLoadCards(
            [],
            [new EqDoctor { IdDoctor = DoctorId, FullName = "Иванов И.И." }],
            new Dictionary<int, DoctorOpenShiftEntry>
            {
                [DoctorId] = new DoctorOpenShiftEntry(DoctorId, 1, "110")
            });

        var card = Assert.Single(cards);
        Assert.False(card.IsInService);
        Assert.Null(card.CurrentTicketNumber);
    }

    private static IReadOnlyList<DoctorLoadCardViewModel> BuildDoctorLoadCards(
        IReadOnlyList<EqAppointment> open,
        IReadOnlyList<EqDoctor> doctorsOrdered,
        IReadOnlyDictionary<int, DoctorOpenShiftEntry> openShiftsByDoctor)
    {
        var method = typeof(QueueDashboardService).GetMethod(
            "BuildDoctorLoadCards",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IReadOnlyList<DoctorLoadCardViewModel>>(
            method.Invoke(null, [open, Now, doctorsOrdered, openShiftsByDoctor]));
    }

    private static EqListItem InServiceStep(int id, int doctorId) =>
        new()
        {
            IdListItem = id,
            IdDoctor = doctorId,
            TimeStartServicing = new TimeOnly(11, 0),
            StatusItem = new EqStatusItemList { Name = "Обслуживается" }
        };
}
