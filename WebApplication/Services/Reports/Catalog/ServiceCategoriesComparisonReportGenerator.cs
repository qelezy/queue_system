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
            select new StageRow(
                li.IdListItem,
                a.IdAppointment,
                a.IdCategory ?? 0,
                c.Name ?? "—",
                a.DateArrival,
                a.TimeArrival,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing)).ToList();

        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>();
        foreach (var appointmentGroup in raw.GroupBy(x => x.IdAppointment))
        {
            var ordered = CatalogReportWaitingHelper.OrderStagesForAppointment(appointmentGroup);
            for (var i = 0; i < ordered.Count; i++)
            {
                var x = ordered[i];
                double? wait = null;
                double? waitExact = null;
                if (x.TimeCall is { } timeCall)
                {
                    waitExact = ServiceCategoriesComparisonReportBuilder.ComputeWaitMinutesExact(
                        x.DateArrival,
                        x.TimeArrival,
                        ordered,
                        i,
                        timeCall);
                    wait = ServiceCategoriesComparisonReportBuilder.ComputeWaitMinutes(
                        x.DateArrival,
                        x.TimeArrival,
                        ordered,
                        i,
                        timeCall) ?? waitExact;
                }

                double? svc = null;
                double? svcExact = null;
                if (x.TimeStartServicing is { } start && x.TimeEndServicing is { } end)
                {
                    svcExact = ServiceCategoriesComparisonReportBuilder.ComputeSvcMinutesExact(
                        x.DateArrival,
                        start,
                        end);
                    svc = ServiceCategoriesComparisonReportBuilder.ComputeSvcMinutes(x.DateArrival, start, end)
                        ?? svcExact;
                }

                observations.Add(new ServiceCategoriesComparisonReportBuilder.CategoryStageObservation(
                    x.IdAppointment,
                    x.IdCategory,
                    x.CategoryName,
                    wait,
                    waitExact,
                    svc,
                    svcExact));
            }
        }

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(observations, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }

    private readonly record struct StageRow(
        int IdListItem,
        int IdAppointment,
        int IdCategory,
        string CategoryName,
        DateOnly DateArrival,
        TimeOnly TimeArrival,
        TimeOnly? TimeCall,
        TimeOnly? TimeStartServicing,
        TimeOnly? TimeEndServicing) : CatalogReportWaitingHelper.IWaitStageRow;
}
