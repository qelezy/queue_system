using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Services.Reports.Catalog;

public sealed class StagesAndWaitingReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.StagesAndWaiting;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (periodFrom, periodTo, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var stages = StagesAndWaitingQueries.LoadStages(
            queue.ListItems.AsNoTracking(),
            queue.Appointments.AsNoTracking(),
            fromDo,
            toDo);

        var model = StagesAndWaitingReportBuilder.BuildReport(stages, periodFrom, periodTo, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }
}
