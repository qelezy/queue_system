using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WebApplication.Models.Reports.Constants;
using WebApplication.Models.Reports.Contracts;
using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

[Trait(ElectronicQueueTestDb.RequiresDbTrait, "true")]
public sealed class ServiceRouteOutcomesLiveConsistencyTests
{
    private static readonly DateOnly FromDo = new(2026, 5, 1);
    private static readonly DateOnly ToDo = new(2026, 5, 19);

    [Fact]
    public async Task ServiceRouteOutcomes_period_completed_and_incomplete_match_database()
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        await using var db = ElectronicQueueTestDb.CreateContext();

        var appointmentIds = await db.Appointments.AsNoTracking()
            .Where(a => a.DateArrival >= FromDo && a.DateArrival <= ToDo)
            .Select(a => a.IdAppointment)
            .ToListAsync();

        var listItems = await db.ListItems.AsNoTracking()
            .Where(li => appointmentIds.Contains(li.IdAppointment))
            .Select(li => new { li.IdAppointment, li.TimeEndServicing })
            .ToListAsync();

        var itemsByAppointment = listItems
            .GroupBy(li => li.IdAppointment)
            .ToDictionary(g => g.Key, g => g.ToList());

        var expectedCompleted = appointmentIds.Count(id =>
        {
            if (!itemsByAppointment.TryGetValue(id, out var stages) || stages.Count == 0)
                return false;
            return stages.All(li => li.TimeEndServicing.HasValue);
        });

        var expectedIncomplete = appointmentIds.Count(id =>
        {
            if (!itemsByAppointment.TryGetValue(id, out var stages) || stages.Count == 0)
                return false;
            return stages.Any(li => !li.TimeEndServicing.HasValue);
        });

        var generator = new ServiceRouteOutcomesReportGenerator();
        var response = generator.Generate(
            BuildRequest(ReportIds.ServiceRouteOutcomes),
            db,
            ReportGenerationPurpose.ExportOrFull);

        Assert.True(response.Implemented);
        var totalsRow = response.Result!.Rows
            .Last(r => string.Equals(r.RowClass, "report-load-table__row--period-total", StringComparison.Ordinal));

        Assert.Equal(expectedCompleted, int.Parse(totalsRow.Cells[3], CultureInfo.InvariantCulture));
        Assert.Equal(expectedIncomplete, int.Parse(totalsRow.Cells[4], CultureInfo.InvariantCulture));
    }

    private static ReportGenerateRequest BuildRequest(string reportId) =>
        new()
        {
            ReportId = reportId,
            DateFrom = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTo = new DateTime(2026, 5, 19, 23, 59, 59, DateTimeKind.Utc)
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
}
