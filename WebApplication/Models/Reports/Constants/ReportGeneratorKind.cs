namespace WebApplication.Models.Reports.Constants;

public enum ReportGeneratorKind
{
    LoadAndDowntime,
    ArrivedAndCompleted,
    WaitingBeforeAppointment,
    AppointmentDuration,
    RouteAndPauses,
    NoShowsAndIncomplete,
    ServiceCategoriesComparison,
    ServiceDelays
}
