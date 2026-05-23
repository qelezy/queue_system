using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Services.Reports.Catalog;

public sealed class ServiceRouteOutcomesReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.ServiceRouteOutcomes;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var categories = queue.Categories.AsNoTracking()
            .ToDictionary(c => c.IdCategory, c => (c.Name ?? "—", c.Priority));

        var (appointments, listItems) = CatalogAppointmentDataLoader.LoadArrivedObservations(queue, fromDo, toDo);

        var model = ServiceRouteOutcomesReportBuilder.BuildReport(
            appointments, listItems, categories, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }
}
