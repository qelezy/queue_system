using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models;

namespace WebApplication.Services.Reports.Catalog;

public sealed class RouteAndPausesReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.RouteAndPauses;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (periodFrom, periodTo, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var stages = RouteAndPausesQueries.LoadStages(
            queue.ListItems.AsNoTracking(),
            queue.Appointments.AsNoTracking(),
            fromDo,
            toDo);

        var model = RouteAndPausesReportBuilder.BuildReport(stages, periodFrom, periodTo, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }
}
