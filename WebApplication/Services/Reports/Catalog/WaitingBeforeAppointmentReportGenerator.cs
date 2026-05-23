using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplication.Data;
using WebApplication.Models.Configuration;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

public sealed class WaitingBeforeAppointmentReportGenerator : IReportGenerator
{
    private readonly MonitoringOptions _monitoring;

    public WaitingBeforeAppointmentReportGenerator(IOptions<MonitoringOptions> monitoring) =>
        _monitoring = monitoring.Value;

    public ReportGeneratorKind Kind => ReportGeneratorKind.WaitingBeforeAppointment;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (periodFrom, periodTo, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var rows = (
            from li in queue.ListItems.AsNoTracking()
            join a in queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            where a.DateArrival >= fromDo && a.DateArrival <= toDo
                  && li.TimeCall != null
            select new CatalogReportWaitingHelper.WaitStageRow(
                li.IdListItem,
                a.IdAppointment,
                a.DateArrival,
                a.TimeArrival,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing)).ToList();

        var observations = CatalogReportWaitingHelper.BuildWaitingObservations(rows, periodFrom, periodTo);

        var model = WaitingBeforeAppointmentReportBuilder.BuildReport(
            observations,
            fromDo,
            toDo,
            periodFrom,
            periodTo,
            purpose,
            _monitoring.WorkdayStartHour,
            _monitoring.WorkdayEndHour);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }
}
