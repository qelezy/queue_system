namespace WebApplication.Services.Dashboard;

public sealed class DoctorPotentialPatientsResponse
{
    public string DoctorName { get; set; } = "";
    public IReadOnlyList<DoctorPotentialPatientDto> Patients { get; set; } = [];
}

public sealed class DoctorPotentialPatientDto
{
    public string TicketNumber { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int Priority { get; set; }
    public int WaitingMinutes { get; set; }
}

public sealed class AppointmentCompletedStagesResponse
{
    public string TicketNumber { get; set; } = "";
    public IReadOnlyList<AppointmentCompletedStageDto> Stages { get; set; } = [];
}

public sealed class AppointmentCompletedStageDto
{
    public string Specialty { get; set; } = "";
    public string Cabinet { get; set; } = "";
    public string TimeCall { get; set; } = "";
    public string TimeStart { get; set; } = "";
    public string TimeEnd { get; set; } = "";
}
