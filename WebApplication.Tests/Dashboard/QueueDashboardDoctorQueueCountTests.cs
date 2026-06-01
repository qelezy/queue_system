using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Dashboard;
using Xunit;

namespace WebApplication.Tests.Dashboard;

public sealed class QueueDashboardDoctorQueueCountTests
{
    private const int DoctorId = 42;

    [Fact]
    public void CountForDoctor_one_ticket_three_open_steps_counts_current_only()
    {
        var appointment = CreateAppointment(1, CreateRouteWithExtraOpenSteps());
        var count = QueueDashboardDoctorQueueCount.CountForDoctor(DoctorId, [appointment]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void CountForDoctor_two_tickets_waiting_for_same_doctor()
    {
        var a1 = CreateAppointment(1, [WaitingStep(1, DoctorId)]);
        var a2 = CreateAppointment(2, [WaitingStep(2, DoctorId)]);
        var count = QueueDashboardDoctorQueueCount.CountForDoctor(DoctorId, [a1, a2]);
        Assert.Equal(2, count);
    }

    [Fact]
    public void CountForDoctor_excludes_in_service_current_step()
    {
        var inService = InServiceStep(3, DoctorId);
        var appointment = CreateAppointment(1, [inService]);
        var count = QueueDashboardDoctorQueueCount.CountForDoctor(DoctorId, [appointment], inService);
        Assert.Equal(0, count);
    }

    [Fact]
    public void CountForDoctor_counts_other_tickets_while_one_in_service()
    {
        var inService = InServiceStep(1, DoctorId);
        var waitingOther = CreateAppointment(2, [WaitingStep(2, DoctorId)]);
        var inServiceAppt = CreateAppointment(1, [inService]);
        var count = QueueDashboardDoctorQueueCount.CountForDoctor(
            DoctorId,
            [inServiceAppt, waitingOther],
            inService);
        Assert.Equal(1, count);
    }

    [Fact]
    public void CountForDoctor_called_current_step_counts()
    {
        var appointment = CreateAppointment(1, [CalledStep(1, DoctorId)]);
        var count = QueueDashboardDoctorQueueCount.CountForDoctor(DoctorId, [appointment]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void CountForDoctor_different_doctor_not_counted()
    {
        var appointment = CreateAppointment(1, [WaitingStep(1, 99)]);
        var count = QueueDashboardDoctorQueueCount.CountForDoctor(DoctorId, [appointment]);
        Assert.Equal(0, count);
    }

    private static List<EqListItem> CreateRouteWithExtraOpenSteps()
    {
        var t = new TimeOnly(10, 0);
        return
        [
            CompletedStep(1, DoctorId, t),
            CompletedStep(2, DoctorId, t),
            WaitingStep(3, DoctorId)
        ];
    }

    private static EqListItem CompletedStep(int id, int doctorId, TimeOnly times) =>
        new()
        {
            IdListItem = id,
            IdDoctor = doctorId,
            TimeStartServicing = times,
            TimeEndServicing = times.AddMinutes(5),
            StatusItem = new EqStatusItemList { Name = "Обслужен" }
        };

    private static EqListItem WaitingStep(int id, int doctorId) =>
        new()
        {
            IdListItem = id,
            IdDoctor = doctorId,
            StatusItem = new EqStatusItemList { Name = "Ожидает" }
        };

    private static EqListItem CalledStep(int id, int doctorId) =>
        new()
        {
            IdListItem = id,
            IdDoctor = doctorId,
            TimeCall = new TimeOnly(11, 0),
            StatusItem = new EqStatusItemList { Name = "Вызван" }
        };

    private static EqListItem InServiceStep(int id, int doctorId) =>
        new()
        {
            IdListItem = id,
            IdDoctor = doctorId,
            TimeStartServicing = new TimeOnly(11, 0),
            StatusItem = new EqStatusItemList { Name = "Обслуживается" }
        };

    private static EqAppointment CreateAppointment(int id, IReadOnlyList<EqListItem> steps)
    {
        var appt = new EqAppointment
        {
            IdAppointment = id,
            DateArrival = new DateOnly(2026, 5, 31),
            TimeArrival = new TimeOnly(9, 0),
            ListItems = steps.ToList()
        };
        foreach (var li in appt.ListItems)
            li.IdAppointment = id;
        return appt;
    }
}
