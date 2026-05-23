namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogAppointmentObservations
{
    internal readonly record struct AppointmentObservation(
        int IdAppointment,
        DateOnly DateArrival,
        int IdCategory);

    internal readonly record struct ListItemObservation(
        int IdAppointment,
        TimeOnly? TimeCall,
        TimeOnly? TimeStartServicing,
        TimeOnly? TimeEndServicing);
}
