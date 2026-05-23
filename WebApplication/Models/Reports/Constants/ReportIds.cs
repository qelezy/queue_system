namespace WebApplication.Models.Reports.Constants;

/// <summary>Идентификаторы отчётов из <c>appsettings.json</c> → <c>Reports:Catalog</c>.</summary>
public static class ReportIds
{
    public const string LoadAndDowntime = "load-and-downtime";
    public const string WaitingBeforeAppointment = "waiting-before-appointment";
    public const string AppointmentDuration = "appointment-duration";
    public const string RouteAndPauses = "route-and-pauses";
    public const string ServiceRouteOutcomes = "service-route-outcomes";
    public const string ServiceCategoriesComparison = "service-categories-comparison";
    public const string ServiceDelays = "service-delays";
}
