using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Services.Reports.Catalog;

namespace WebApplication.Services.Reports.LoadAndDowntime;

public sealed class LoadAndDowntimeReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.LoadAndDowntime;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        if (!DateTime.TryParse(
                request.DateFrom,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodFrom)
            || !DateTime.TryParse(
                request.DateTo,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodTo))
        {
            periodFrom = DateTime.UtcNow.Date.AddDays(-7);
            periodTo = DateTime.UtcNow;
        }

        if (periodFrom > periodTo)
            (periodFrom, periodTo) = (periodTo, periodFrom);

        var byCabinet = request.CustomParams is not null
                        && request.CustomParams.TryGetValue("analysisMode", out var am)
                        && string.Equals(am?.Trim(), "cabinet", StringComparison.OrdinalIgnoreCase);

        var fromDo = DateOnly.FromDateTime(periodFrom);
        var toDoOnly = DateOnly.FromDateTime(periodTo);

        var rawLogs = queue.LogWorks.AsNoTracking()
            .Where(lw => lw.TimeBegin != null && lw.TimeEnd != null
                         && lw.DateWork >= fromDo && lw.DateWork <= toDoOnly)
            .Select(lw => new LoadAndDowntimeReportBuilder.LogWorkLite(
                lw.IdDoctor,
                lw.IdCabinet,
                lw.DateWork,
                lw.TimeBegin!.Value,
                lw.TimeEnd!.Value))
            .ToList();

        var listRows = (
            from li in queue.ListItems.AsNoTracking()
            join a in queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            join st in queue.StatusItemLists.AsNoTracking() on li.IdStatusItem equals st.IdStatusItem
            join sp in queue.Specialties.AsNoTracking() on li.IdSpecialty equals sp.IdSpecialty
            where a.DateArrival >= fromDo && a.DateArrival <= toDoOnly
                  && li.TimeStartServicing != null
                  && li.TimeEndServicing != null
            select new LoadAndDowntimeReportBuilder.ListRowLite(
                li.IdAppointment,
                li.IdDoctor,
                li.IdCabinet,
                a.DateArrival,
                li.IdStatusItem,
                st.Name,
                li.TimeCall,
                li.TimeStartServicing!.Value,
                li.TimeEndServicing!.Value,
                sp.Definition))
            .ToList();

        var doctors = queue.Doctors.AsNoTracking().ToDictionary(d => d.IdDoctor, d => d.FullName);
        var cabinets = queue.Cabinets.AsNoTracking().ToDictionary(c => c.IdCabinet, c => c.CabinetNumber);

        var result = LoadAndDowntimeReportBuilder.BuildReport(
            rawLogs,
            listRows,
            doctors,
            cabinets,
            periodFrom,
            periodTo,
            byCabinet,
            purpose);

        return new ReportGenerateResponse { Success = true, Implemented = true, Result = result };
    }
}
