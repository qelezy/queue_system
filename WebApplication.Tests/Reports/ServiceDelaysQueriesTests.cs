using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ServiceDelaysQueriesTests
{
    private static readonly DateOnly Day = new(2026, 6, 1);

    [Fact]
    public void BuildEntityMetrics_ignores_call_to_start_when_within_norm()
    {
        var stages = new[]
        {
            Stage(
                idDoctor: 1,
                timeCall: new TimeOnly(10, 0),
                timeStart: new TimeOnly(10, 5),
                timeEnd: new TimeOnly(10, 15),
                timeServicing: 20)
        };

        var metrics = ServiceDelaysQueries.BuildEntityMetrics(
            stages,
            new Dictionary<int, string> { [1] = "Dr A" });

        Assert.Empty(metrics);
    }

    [Fact]
    public void BuildEntityMetrics_counts_only_over_norm_minutes()
    {
        var stages = new[]
        {
            Stage(
                idDoctor: 1,
                timeCall: new TimeOnly(9, 0),
                timeStart: new TimeOnly(9, 0),
                timeEnd: new TimeOnly(9, 30),
                timeServicing: 20)
        };

        var metrics = ServiceDelaysQueries.BuildEntityMetrics(
            stages,
            new Dictionary<int, string> { [1] = "Dr A" });

        var m = Assert.Single(metrics);
        Assert.Equal(1, m.OverNormCount);
        Assert.Equal(10, m.TotalDelayMin, 2);
        Assert.Equal(10, m.MinDelayMin, 2);
        Assert.Equal(10, m.MaxDelayMin, 2);
        Assert.Equal(10, m.AvgDelayMin!.Value, 2);
    }

    [Fact]
    public void BuildEntityMetrics_call_delay_plus_over_norm_counts_only_excess()
    {
        var stages = new[]
        {
            Stage(
                idDoctor: 2,
                timeCall: new TimeOnly(11, 0),
                timeStart: new TimeOnly(11, 10),
                timeEnd: new TimeOnly(11, 35),
                timeServicing: 20)
        };

        var metrics = ServiceDelaysQueries.BuildEntityMetrics(
            stages,
            new Dictionary<int, string> { [2] = "Dr B" });

        var m = Assert.Single(metrics);
        Assert.Equal(1, m.OverNormCount);
        Assert.Equal(5, m.TotalDelayMin, 2);
    }

    [Fact]
    public void BuildEntityMetrics_subminute_over_norm_not_counted()
    {
        var stages = new[]
        {
            Stage(
                idDoctor: 3,
                timeCall: new TimeOnly(12, 0),
                timeStart: new TimeOnly(12, 0),
                timeEnd: new TimeOnly(12, 20, 15),
                timeServicing: 20)
        };

        var metrics = ServiceDelaysQueries.BuildEntityMetrics(
            stages,
            new Dictionary<int, string> { [3] = "Dr C" });

        Assert.Empty(metrics);
    }

    [Fact]
    public void BuildEntityMetrics_one_whole_minute_over_norm()
    {
        var stages = new[]
        {
            Stage(
                idDoctor: 4,
                timeCall: null,
                timeStart: new TimeOnly(14, 0),
                timeEnd: new TimeOnly(14, 21),
                timeServicing: 20)
        };

        var metrics = ServiceDelaysQueries.BuildEntityMetrics(
            stages,
            new Dictionary<int, string> { [4] = "Dr D" });

        var m = Assert.Single(metrics);
        Assert.Equal(1, m.OverNormCount);
        Assert.Equal(1, m.TotalDelayMin);
        Assert.Equal("1 мин", CatalogReportShared.FormatDuration(m.MinDelayMin));
    }

    [Fact]
    public void BuildReport_has_seven_columns_without_incidents()
    {
        var metrics = new List<ServiceDelaysQueries.EntityMetrics>
        {
            new(1, "Dr A", "Therapy", 10, 10, 10, 10, 1, 10, 10, 10, 10)
        };

        var model = ServiceDelaysReportBuilder.BuildReport(
            metrics,
            ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(7, model.ColumnHeaders.Count);
        Assert.DoesNotContain(model.ColumnHeaders, h => h.Contains("Инцидент", StringComparison.Ordinal));
        Assert.Equal(7, model.Rows[0].Cells!.Count);
    }

    private static ServiceDelaysQueries.StageObservation Stage(
        int idDoctor,
        TimeOnly? timeCall,
        TimeOnly timeStart,
        TimeOnly timeEnd,
        int timeServicing) =>
        new(
            1,
            100,
            Day,
            idDoctor,
            null,
            timeCall,
            timeStart,
            timeEnd,
            timeServicing,
            "Therapy");
}
