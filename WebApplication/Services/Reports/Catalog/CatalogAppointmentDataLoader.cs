using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogAppointmentDataLoader
{
    internal static (
        List<CatalogAppointmentObservations.AppointmentObservation> Appointments,
        List<CatalogAppointmentObservations.ListItemObservation> ListItems)
        LoadArrivedObservations(ElectronicQueueDbContext queue, DateOnly fromDo, DateOnly toDo)
    {
        var appointments = queue.Appointments.AsNoTracking()
            .Where(a => a.DateArrival >= fromDo && a.DateArrival <= toDo)
            .Select(a => new CatalogAppointmentObservations.AppointmentObservation(
                a.IdAppointment,
                a.DateArrival,
                a.IdCategory ?? 0))
            .ToList();

        var appIds = appointments.Select(a => a.IdAppointment).ToHashSet();
        var listItems = queue.ListItems.AsNoTracking()
            .Where(li => appIds.Contains(li.IdAppointment))
            .Select(li => new CatalogAppointmentObservations.ListItemObservation(
                li.IdAppointment,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing))
            .ToList();

        return (appointments, listItems);
    }
}
