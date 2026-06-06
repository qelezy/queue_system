using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Services.Reports.Catalog;

public sealed class ServiceDelaysReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.ServiceDelays;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var stages = (
            from li in queue.ListItems.AsNoTracking()
            join a in queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            join sp in queue.Specialties.AsNoTracking() on li.IdSpecialty equals sp.IdSpecialty
            where a.DateArrival >= fromDo && a.DateArrival <= toDo
            select new ServiceDelaysQueries.StageObservation(
                li.IdListItem,
                li.IdAppointment,
                a.DateArrival,
                li.IdDoctor,
                li.IdCabinet,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing,
                sp.TimeServicing,
                sp.Definition)).ToList();

        var entityLabels = queue.Doctors.AsNoTracking()
            .ToDictionary(
                d => d.IdDoctor,
                d => AppointmentDurationReportBuilder.NormalizeDimensionLabel(d.FullName));

        var metrics = ServiceDelaysQueries.BuildEntityMetrics(stages, entityLabels);

        var model = ServiceDelaysReportBuilder.BuildReport(metrics, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }
}
