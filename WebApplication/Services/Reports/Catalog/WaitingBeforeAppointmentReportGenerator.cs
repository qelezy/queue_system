using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

public sealed class WaitingBeforeAppointmentReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.WaitingBeforeAppointment;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (periodFrom, periodTo, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var raw = (
            from li in queue.ListItems.AsNoTracking()
            join a in queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            where a.DateArrival >= fromDo && a.DateArrival <= toDo
                  && li.TimeCall != null
            select new
            {
                a.DateArrival,
                a.TimeArrival,
                Call = li.TimeCall!.Value
            }).ToList();

        var observations = raw
            .Where(x => WaitingBeforeAppointmentReportBuilder.IsCallInPeriod(
                x.DateArrival, x.Call, periodFrom, periodTo))
            .Select(x => new WaitingBeforeAppointmentReportBuilder.WaitingObservation(
                x.DateArrival,
                x.TimeArrival.Hour,
                WaitBeforeCallMinutes(x.DateArrival, x.TimeArrival, x.Call)))
            .Where(x => x.WaitMin >= 0 && x.WaitMin < 10080)
            .ToList();

        var model = WaitingBeforeAppointmentReportBuilder.BuildReport(
            observations, fromDo, toDo, periodFrom, periodTo, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }

    private static double WaitBeforeCallMinutes(DateOnly dateArrival, TimeOnly timeArrival, TimeOnly timeCall) =>
        (EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeCall)
         - EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeArrival)).TotalMinutes;
}
