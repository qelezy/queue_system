using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class CatalogReportWaitingHelperTests
{
    private static readonly DateOnly Day = new(2026, 5, 10);

    private sealed record Stage(
        int IdListItem,
        TimeOnly? TimeCall,
        TimeOnly? TimeStartServicing,
        TimeOnly? TimeEndServicing) : CatalogReportWaitingHelper.IWaitStageRow;

    [Fact]
    public void TryComputeWait_first_stage_uses_arrival()
    {
        var stages = new List<Stage>
        {
            new(1, new TimeOnly(8, 15), new TimeOnly(8, 15), new TimeOnly(8, 45))
        };

        var wait = CatalogReportWaitingHelper.TryComputeWaitBeforeCallMinutes(
            Day,
            new TimeOnly(8, 0),
            stages,
            0,
            new TimeOnly(8, 15));

        Assert.Equal(15, wait);
    }

    [Fact]
    public void TryComputeWait_second_stage_uses_previous_end_not_arrival()
    {
        var stages = new List<Stage>
        {
            new(1, new TimeOnly(8, 15), new TimeOnly(8, 15), new TimeOnly(10, 0)),
            new(2, new TimeOnly(10, 15), new TimeOnly(10, 15), new TimeOnly(10, 30))
        };

        var wait = CatalogReportWaitingHelper.TryComputeWaitBeforeCallMinutes(
            Day,
            new TimeOnly(8, 0),
            stages,
            1,
            new TimeOnly(10, 15));

        Assert.Equal(15, wait);
    }

    [Fact]
    public void TryComputeWait_second_stage_falls_back_to_previous_start()
    {
        var stages = new List<Stage>
        {
            new(1, new TimeOnly(9, 10), new TimeOnly(9, 10), null),
            new(2, new TimeOnly(9, 40), new TimeOnly(9, 40), new TimeOnly(10, 0))
        };

        var wait = CatalogReportWaitingHelper.TryComputeWaitBeforeCallMinutes(
            Day,
            new TimeOnly(9, 0),
            stages,
            1,
            new TimeOnly(9, 40));

        Assert.Equal(30, wait);
    }

    [Fact]
    public void TryComputeWait_second_stage_skipped_when_previous_has_no_times()
    {
        var stages = new List<Stage>
        {
            new(1, null, null, null),
            new(2, new TimeOnly(10, 15), new TimeOnly(10, 15), new TimeOnly(10, 30))
        };

        var wait = CatalogReportWaitingHelper.TryComputeWaitBeforeCallMinutes(
            Day,
            new TimeOnly(8, 0),
            stages,
            1,
            new TimeOnly(10, 15));

        Assert.Null(wait);
    }

    [Fact]
    public void BuildWaitingObservations_groups_by_appointment_and_filters_period()
    {
        var periodFrom = Day.ToDateTime(new TimeOnly(0, 0));
        var periodTo = Day.ToDateTime(new TimeOnly(23, 59));
        var rows = new List<CatalogReportWaitingHelper.WaitStageRow>
        {
            new(1, 1, Day, new TimeOnly(8, 0), new TimeOnly(8, 15), new TimeOnly(8, 15), new TimeOnly(10, 0)),
            new(2, 1, Day, new TimeOnly(8, 0), new TimeOnly(10, 15), new TimeOnly(10, 15), new TimeOnly(10, 30))
        };

        var observations = CatalogReportWaitingHelper.BuildWaitingObservations(rows, periodFrom, periodTo);

        Assert.Equal(2, observations.Count);
        Assert.Contains(observations, o => o.WaitMin == 15 && o.Hour == 8);
        Assert.Contains(observations, o => o.WaitMin == 15 && o.Hour == 8);
    }

    [Fact]
    public void OrderStages_tiebreaks_equal_start_by_id_list_item()
    {
        var sameStart = new TimeOnly(10, 0);
        var stages = new List<Stage>
        {
            new(20, new TimeOnly(10, 30), sameStart, new TimeOnly(10, 20)),
            new(10, new TimeOnly(10, 15), sameStart, new TimeOnly(10, 10))
        };

        var ordered = CatalogReportWaitingHelper.OrderStagesForAppointment(stages);

        Assert.Equal(10, ordered[0].IdListItem);
        Assert.Equal(20, ordered[1].IdListItem);
        var wait = CatalogReportWaitingHelper.TryComputeWaitBeforeCallMinutes(
            Day,
            new TimeOnly(8, 0),
            ordered,
            1,
            new TimeOnly(10, 30));
        Assert.Equal(20, wait);
    }
}
