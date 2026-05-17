using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Services.Reports.Catalog;

public sealed class ServiceCategoriesComparisonReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.ServiceCategoriesComparison;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var raw = (
            from li in queue.ListItems.AsNoTracking()
            join a in queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            join c in queue.Categories.AsNoTracking() on a.IdCategory equals c.IdCategory
            where a.DateArrival >= fromDo && a.DateArrival <= toDo
            select new
            {
                a.IdAppointment,
                a.IdCategory,
                c.Name,
                a.DateArrival,
                a.TimeArrival,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing
            }).ToList();

        var observations = raw.Select(x =>
        {
            double? wait = null;
            if (x.TimeCall.HasValue)
                wait = ServiceCategoriesComparisonReportBuilder.ComputeWaitMinutes(
                    x.DateArrival, x.TimeArrival, x.TimeCall.Value);

            double? svc = null;
            if (x.TimeStartServicing.HasValue && x.TimeEndServicing.HasValue)
                svc = ServiceCategoriesComparisonReportBuilder.ComputeSvcMinutes(
                    x.DateArrival, x.TimeStartServicing.Value, x.TimeEndServicing.Value);

            return new ServiceCategoriesComparisonReportBuilder.CategoryStageObservation(
                x.IdAppointment,
                x.IdCategory,
                x.Name ?? "—",
                wait,
                svc);
        }).ToList();

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(observations, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }
}
