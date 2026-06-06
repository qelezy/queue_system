using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Models.Reports.Constants;
using WebApplication.Models.Reports.Contracts;
using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

[Trait(ElectronicQueueTestDb.RequiresDbTrait, "true")]
public sealed class ReportWaitingLiveConsistencyTests
{
    private static readonly DateOnly Day = new(2026, 5, 18);

    private static readonly (int Hour, int Count, double Avg, double Min, double Max)[] ExpectedWorkdayHours =
    [
        (8, 150, 13.0, 0, 192.4),
        (9, 175, 12.5, 0, 199.4),
        (10, 156, 11.7, 0, 168.9),
        (11, 115, 5.1, 0, 51.3),
        (12, 78, 3.3, 0, 24.8),
        (13, 27, 3.4, 0, 24.5),
        (14, 3, 4.3, 39.0 / 60.0, 11.0),
        (15, 4, 6.8, 1.0, 15.6)
    ];

    [Fact]
    public async Task WaitingBeforeAppointment_2026_05_18_hourly_metrics_match_sql_baseline()
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        await using var db = ElectronicQueueTestDb.CreateContext();
        var generator = new WaitingBeforeAppointmentReportGenerator();
        var response = generator.Generate(
            BuildDayRequest(),
            db,
            ReportGenerationPurpose.ExportOrFull);

        Assert.True(response.Implemented);
        Assert.NotNull(response.Result);

        var detailRows = response.Result!.Rows
            .Where(r => string.IsNullOrWhiteSpace(r.RowClass))
            .ToList();

        foreach (var expected in ExpectedWorkdayHours)
        {
            var row = detailRows.Single(r =>
                r.Cells![1].StartsWith(expected.Hour.ToString("00", CultureInfo.InvariantCulture) + ":00", StringComparison.Ordinal));
            Assert.Equal(expected.Count.ToString(CultureInfo.InvariantCulture), row.Cells[2]);
            ReportsDurationTestHelper.AssertDurationCell(expected.Avg, row.Cells[3]);
            ReportsDurationTestHelper.AssertDurationCell(expected.Min, row.Cells[4]);
            ReportsDurationTestHelper.AssertDurationCell(expected.Max, row.Cells[5]);
        }

        var dayTotalRow = response.Result.Rows
            .Last(r => string.Equals(r.RowClass, "report-load-table__row--day-totals-end", StringComparison.Ordinal));
        Assert.Equal("790", dayTotalRow.Cells![2]);
    }

    [Fact]
    public async Task WaitingBeforeAppointment_uses_inter_stage_wait_not_arrival_for_late_stages()
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        await using var db = ElectronicQueueTestDb.CreateContext();
        var rows = await (
            from li in db.ListItems.AsNoTracking()
            join a in db.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            where a.DateArrival == Day && li.TimeCall != null
            select new CatalogReportWaitingHelper.WaitStageRow(
                li.IdListItem,
                a.IdAppointment,
                a.DateArrival,
                a.TimeArrival,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing)).ToListAsync();

        var periodFrom = Day.ToDateTime(new TimeOnly(0, 0));
        var periodTo = Day.ToDateTime(new TimeOnly(23, 59, 59));
        var observations = CatalogReportWaitingHelper.BuildWaitingObservations(rows, periodFrom, periodTo);

        var multiStage = rows.GroupBy(x => x.IdAppointment).Where(g => g.Count() >= 2).ToList();
        Assert.NotEmpty(multiStage);

        foreach (var appointmentGroup in multiStage.Take(20))
        {
            var ordered = CatalogReportWaitingHelper.OrderStagesForAppointment(appointmentGroup);
            for (var i = 1; i < ordered.Count; i++)
            {
                var stage = ordered[i];
                if (stage.TimeCall is not { } timeCall)
                    continue;

                var wait = CatalogReportWaitingHelper.TryComputeWaitBeforeCallMinutes(
                    stage.DateArrival,
                    stage.TimeArrival,
                    ordered,
                    i,
                    timeCall);
                if (wait is null)
                    continue;

                var legacyFromArrival = (EqDateTimeExtensions.CombineOnArrivalDate(stage.DateArrival, timeCall)
                    - EqDateTimeExtensions.CombineOnArrivalDate(stage.DateArrival, stage.TimeArrival)).TotalMinutes;
                if (legacyFromArrival - wait.Value > 30)
                    Assert.True(wait.Value < legacyFromArrival);
            }
        }
    }

    private static ReportGenerateRequest BuildDayRequest() =>
        new()
        {
            ReportId = ReportIds.WaitingBeforeAppointment,
            DateFrom = Day.ToDateTime(new TimeOnly(0, 0)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTo = Day.ToDateTime(new TimeOnly(23, 59, 59)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
}
