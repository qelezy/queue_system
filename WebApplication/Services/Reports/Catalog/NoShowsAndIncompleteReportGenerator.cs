using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Services.Reports.Catalog;

public sealed class NoShowsAndIncompleteReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.NoShowsAndIncomplete;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var categories = queue.Categories.AsNoTracking()
            .ToDictionary(c => c.IdCategory, c => (c.Name ?? "—", c.Priority));

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

        var model = NoShowsAndIncompleteReportBuilder.BuildReport(
            appointments,
            listItems,
            categories,
            purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }
}
