using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogAppointmentDataLoader
{
    internal static (
        List<ArrivedAndCompletedReportBuilder.ArrivedAppointmentObservation> Appointments,
        List<ArrivedAndCompletedReportBuilder.ArrivedListItemObservation> ListItems)
        LoadArrivedObservations(ElectronicQueueDbContext queue, DateOnly fromDo, DateOnly toDo)
    {
        var appointments = queue.Appointments.AsNoTracking()
            .Where(a => a.DateArrival >= fromDo && a.DateArrival <= toDo)
            .Select(a => new ArrivedAndCompletedReportBuilder.ArrivedAppointmentObservation(
                a.IdAppointment,
                a.DateArrival,
                a.IdCategory))
            .ToList();

        var appIds = appointments.Select(a => a.IdAppointment).ToHashSet();
        var listItems = queue.ListItems.AsNoTracking()
            .Where(li => appIds.Contains(li.IdAppointment))
            .Select(li => new ArrivedAndCompletedReportBuilder.ArrivedListItemObservation(
                li.IdAppointment,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing))
            .ToList();

        return (appointments, listItems);
    }
}
