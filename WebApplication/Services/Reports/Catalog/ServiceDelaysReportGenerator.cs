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
        var analysisMode = ServiceDelaysReportBuilder.ParseAnalysisMode(request.CustomParams);

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

        var entityLabels = BuildEntityLabels(queue, analysisMode);
        var metrics = ServiceDelaysQueries.BuildEntityMetrics(stages, entityLabels, analysisMode);

        var model = ServiceDelaysReportBuilder.BuildReport(metrics, analysisMode, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }

    private static Dictionary<int, string> BuildEntityLabels(
        ElectronicQueueDbContext queue,
        string analysisMode)
    {
        if (string.Equals(analysisMode, ServiceDelaysReportBuilder.ModeCabinet, StringComparison.OrdinalIgnoreCase))
        {
            return queue.Cabinets.AsNoTracking()
                .ToDictionary(
                    c => c.IdCabinet,
                    c => ServiceDelaysReportBuilder.FormatCabinetLabel(c.CabinetNumber));
        }

        return queue.Doctors.AsNoTracking()
            .ToDictionary(
                d => d.IdDoctor,
                d => AppointmentDurationReportBuilder.NormalizeDimensionLabel(d.FullName));
    }
}
