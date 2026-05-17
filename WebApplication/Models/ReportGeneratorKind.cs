namespace WebApplication.Models;

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
