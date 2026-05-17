using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

public sealed class AppointmentDurationReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.AppointmentDuration;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);
        var analysisMode = AppointmentDurationReportBuilder.ParseAnalysisMode(request.CustomParams);
        var observations = LoadObservations(queue, fromDo, toDo, analysisMode);
        var model = AppointmentDurationReportBuilder.BuildReport(
            observations, fromDo, toDo, analysisMode, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }

    private static List<AppointmentDurationReportBuilder.DurationObservation> LoadObservations(
        ElectronicQueueDbContext queue,
        DateOnly fromDo,
        DateOnly toDo,
        string analysisMode)
    {
        var raw = (
            from li in queue.ListItems.AsNoTracking()
            join a in queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            join sp in queue.Specialties.AsNoTracking() on li.IdSpecialty equals sp.IdSpecialty
            where a.DateArrival >= fromDo && a.DateArrival <= toDo
                  && li.TimeStartServicing != null
                  && li.TimeEndServicing != null
            select new
            {
                a.DateArrival,
                Start = li.TimeStartServicing!.Value,
                End = li.TimeEndServicing!.Value,
                li.IdAppointment,
                li.IdDoctor,
                li.IdCabinet,
                sp.Definition,
                sp.TimeServicing
            }).ToList();

        var doctors = queue.Doctors.AsNoTracking().ToDictionary(d => d.IdDoctor, d => d.FullName);
        var cabinets = queue.Cabinets.AsNoTracking().ToDictionary(c => c.IdCabinet, c => c.CabinetNumber);

        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>(raw.Count);
        foreach (var x in raw)
        {
            var svcMin = CatalogReportAnalysisHelper.ComputeSvcMinutes(x.DateArrival, x.Start, x.End);
            if (svcMin is null)
                continue;

            var label = analysisMode switch
            {
                AppointmentDurationReportBuilder.ModeSpecialty =>
                    AppointmentDurationReportBuilder.NormalizeDimensionLabel(x.Definition),
                AppointmentDurationReportBuilder.ModeCabinet =>
                    AppointmentDurationReportBuilder.FormatCabinetLabel(
                        cabinets.TryGetValue(x.IdCabinet, out var num) ? num : ""),
                _ =>
                    AppointmentDurationReportBuilder.NormalizeDimensionLabel(
                        doctors.TryGetValue(x.IdDoctor, out var name) ? name : null)
            };

            observations.Add(new AppointmentDurationReportBuilder.DurationObservation(
                x.DateArrival,
                label,
                x.IdAppointment,
                svcMin.Value,
                x.TimeServicing,
                x.Definition));
        }

        return observations;
    }

}
