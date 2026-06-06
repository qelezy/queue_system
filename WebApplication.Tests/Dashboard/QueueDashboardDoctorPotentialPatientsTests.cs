using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Dashboard;
using Xunit;

namespace WebApplication.Tests.Dashboard;

public sealed class QueueDashboardDoctorPotentialPatientsTests
{
    private const int DoctorId = 42;
    private static readonly DateTime Now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildForDoctor_maps_category_name_and_category_priority()
    {
        var appointment = CreateAppointment(1, "A-101", "Платный", 2, 5, [WaitingStep(1, DoctorId)]);

        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, [appointment], Now);

        Assert.Single(patients);
        Assert.Equal("A-101", patients[0].TicketNumber);
        Assert.Equal("Платный", patients[0].CategoryName);
        Assert.Equal(5, patients[0].Priority);
        Assert.Equal(180, patients[0].WaitingMinutes);
    }

    [Fact]
    public void BuildForDoctor_empty_category_becomes_dash()
    {
        var appointment = CreateAppointment(1, "B-1", null, 0, 0, [WaitingStep(1, DoctorId)]);

        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, [appointment], Now);

        Assert.Single(patients);
        Assert.Equal("—", patients[0].CategoryName);
        Assert.Equal(0, patients[0].Priority);
    }

    [Fact]
    public void BuildForDoctor_excludes_in_service_current_step()
    {
        var appointment = CreateAppointment(1, "C-1", "ОМС", 1, 1, [InServiceStep(1, DoctorId)]);

        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, [appointment], Now);

        Assert.Empty(patients);
    }

    [Fact]
    public void BuildForDoctor_includes_called_current_step()
    {
        var appointment = CreateAppointment(1, "D-1", "ОМС", 1, 1, [CalledStep(1, DoctorId)]);

        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, [appointment], Now);

        Assert.Single(patients);
        Assert.Equal(30, patients[0].WaitingMinutes);
    }

    [Fact]
    public void BuildForDoctor_waiting_minutes_from_previous_stage_end_when_not_called()
    {
        var appointment = CreateAppointment(
            1,
            "M-1",
            "ОМС",
            1,
            1,
            [CompletedStep(1, 99, new TimeOnly(11, 0)), WaitingStep(2, DoctorId)]);

        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, [appointment], Now);

        Assert.Single(patients);
        Assert.Equal(60, patients[0].WaitingMinutes);
    }

    [Fact]
    public void BuildForDoctor_includes_called_step_when_appointment_on_pause()
    {
        var appointment = CreateAppointment(
            1,
            "P-1",
            "ОМС",
            1,
            1,
            [CalledStep(1, DoctorId)],
            5,
            "На паузе");

        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, [appointment], Now);

        Assert.Single(patients);
        Assert.Equal("P-1", patients[0].TicketNumber);
    }

    [Fact]
    public void BuildForDoctor_different_doctor_not_included()
    {
        var appointment = CreateAppointment(1, "E-1", "ОМС", 1, 1, [WaitingStep(1, 99)]);

        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, [appointment], Now);

        Assert.Empty(patients);
    }

    [Fact]
    public void BuildForDoctor_sorts_by_category_priority_desc()
    {
        var low = CreateAppointment(1, "L-1", "ОМС", 3, 1, [WaitingStep(1, DoctorId)]);
        var high = CreateAppointment(2, "H-1", "Платный", 0, 5, [WaitingStep(2, DoctorId)]);

        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, [low, high], Now);

        Assert.Equal(2, patients.Count);
        Assert.Equal("H-1", patients[0].TicketNumber);
        Assert.Equal("L-1", patients[1].TicketNumber);
    }

    [Fact]
    public void BuildForDoctor_count_matches_queue_length()
    {
        var inService = InServiceStep(1, DoctorId);
        var waiting = CreateAppointment(2, "W-1", "ОМС", 1, 1, [WaitingStep(2, DoctorId)]);
        var called = CreateAppointment(3, "C-1", "Платный", 0, 2, [CalledStep(3, DoctorId)]);
        var inServiceAppt = CreateAppointment(1, "S-1", "ОМС", 1, 1, [inService]);
        var otherDoctor = CreateAppointment(4, "X-1", "ОМС", 1, 1, [WaitingStep(4, 99)]);

        var open = new[] { inServiceAppt, waiting, called, otherDoctor };
        var patients = QueueDashboardDoctorPotentialPatients.BuildForDoctor(DoctorId, open, Now);
        var count = QueueDashboardDoctorQueueCount.CountForDoctor(DoctorId, open, inService);

        Assert.Equal(2, patients.Count);
        Assert.Equal(count, patients.Count);
    }

    private static EqListItem CompletedStep(int id, int doctorId, TimeOnly endTime) =>
        new()
        {
            IdListItem = id,
            IdDoctor = doctorId,
            TimeEndServicing = endTime,
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
            TimeCall = new TimeOnly(11, 30),
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

    private static EqAppointment CreateAppointment(
        int id,
        string number,
        string? categoryName,
        int ticketPriority,
        int categoryPriority,
        IReadOnlyList<EqListItem> steps,
        int idStatusApp = 1,
        string statusName = "Ожидает")
    {
        var appt = new EqAppointment
        {
            IdAppointment = id,
            IdStatusApp = idStatusApp,
            Number = number,
            Priority = ticketPriority,
            DateArrival = new DateOnly(2026, 6, 5),
            TimeArrival = new TimeOnly(9, 0),
            StatusAppointment = new EqStatusAppointment { IdStatusApp = idStatusApp, Name = statusName },
            Category = categoryName == null
                ? null!
                : new EqCategory { Name = categoryName, Priority = categoryPriority },
            ListItems = steps.ToList()
        };
        foreach (var li in appt.ListItems)
            li.IdAppointment = id;
        return appt;
    }
}
