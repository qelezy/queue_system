using Microsoft.EntityFrameworkCore;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Dashboard;

internal sealed record DoctorOpenShiftEntry(int IdDoctor, int IdCabinet, string CabinetNumber);

internal static class QueueDashboardDoctorsOnShiftQuery
{
    internal static Task<int> CountAsync(
        IQueryable<EqLogWork> logWorks,
        DateOnly todayDo,
        CancellationToken cancellationToken = default) =>
        logWorks
            .Where(lw => lw.DateWork == todayDo
                         && lw.TimeBegin != null
                         && lw.TimeEnd == null
                         && lw.IdDoctor > 0)
            .Select(lw => lw.IdDoctor)
            .Distinct()
            .CountAsync(cancellationToken);

    internal static async Task<IReadOnlyDictionary<int, DoctorOpenShiftEntry>> LoadOpenShiftsByDoctorAsync(
        IQueryable<EqLogWork> logWorks,
        DateOnly todayDo,
        CancellationToken cancellationToken = default)
    {
        var rows = await logWorks
            .Include(lw => lw.Cabinet)
            .Where(lw => lw.DateWork == todayDo
                         && lw.TimeBegin != null
                         && lw.TimeEnd == null
                         && lw.IdDoctor > 0)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(lw => lw.IdDoctor)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var best = g.OrderByDescending(lw => lw.IdLogWork).First();
                    var cabinetNumber = best.Cabinet?.CabinetNumber?.Trim() ?? "";
                    return new DoctorOpenShiftEntry(best.IdDoctor, best.IdCabinet, cabinetNumber);
                });
    }

    internal static Task<int> CountTotalDoctorsAsync(
        IQueryable<EqDoctor> doctors,
        CancellationToken cancellationToken = default) =>
        doctors.Where(d => d.IdDoctor > 0).CountAsync(cancellationToken);
}
