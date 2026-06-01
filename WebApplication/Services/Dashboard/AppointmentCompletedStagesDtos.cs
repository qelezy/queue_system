namespace WebApplication.Services.Dashboard;

public sealed class AppointmentCompletedStagesResponse
{
    public string TicketNumber { get; set; } = "";
    public IReadOnlyList<AppointmentCompletedStageDto> Stages { get; set; } = [];
}

public sealed class AppointmentCompletedStageDto
{
    public string Specialty { get; set; } = "";
    public string Cabinet { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string StatusCode { get; set; } = "";
    public string TimeCall { get; set; } = "";
    public string TimeStart { get; set; } = "";
    public string TimeEnd { get; set; } = "";
}
