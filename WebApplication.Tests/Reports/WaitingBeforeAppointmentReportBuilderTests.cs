using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class WaitingBeforeAppointmentReportBuilderTests
{
    private static readonly DateOnly Day = new(2026, 5, 10);
    private static readonly DateOnly EmptyDay = new(2026, 5, 11);
    private static readonly DateOnly GapDay = new(2026, 5, 12);

    [Fact]
    public void Build_skips_day_without_observations()
    {
        var periodFrom = Day.ToDateTime(new TimeOnly(8, 0));
        var periodTo = Day.AddDays(2).ToDateTime(new TimeOnly(18, 0));
        var observations = new List<WaitingBeforeAppointmentReportBuilder.WaitingObservation>
        {
            new(Day, 10, 12.0)
        };

        var model = WaitingBeforeAppointmentReportBuilder.Build(
            observations,
            Day,
            GapDay,
            periodFrom,
            periodTo);

        Assert.DoesNotContain(model.Rows, r => (r.Cells?[0] ?? "").Contains("2026-05-11"));
        var chart = Assert.Single(model.PreviewCharts!);
        Assert.Single(chart.Labels);
    }

    [Fact]
    public void Build_shows_gap_hour_with_zero_between_data_hours()
    {
        var periodFrom = Day.ToDateTime(new TimeOnly(8, 0));
        var periodTo = Day.ToDateTime(new TimeOnly(18, 0));
        var observations = new List<WaitingBeforeAppointmentReportBuilder.WaitingObservation>
        {
            new(Day, 10, 12.0),
            new(Day, 12, 8.0)
        };

        var model = WaitingBeforeAppointmentReportBuilder.Build(
            observations,
            Day,
            Day,
            periodFrom,
            periodTo);

        var detailRows = model.Rows
            .Where(r => string.IsNullOrWhiteSpace(r.RowClass))
            .ToList();
        Assert.Equal(3, detailRows.Count);
        Assert.Equal("0", detailRows[1].Cells[2]);
        Assert.Equal("—", detailRows[1].Cells[3]);
    }

    [Fact]
    public void Build_chart_uses_nan_outside_day_hour_range()
    {
        var periodFrom = Day.ToDateTime(new TimeOnly(8, 0));
        var periodTo = GapDay.ToDateTime(new TimeOnly(18, 0));
        var observations = new List<WaitingBeforeAppointmentReportBuilder.WaitingObservation>
        {
            new(Day, 10, 12.0),
            new(GapDay, 14, 9.0)
        };

        var model = WaitingBeforeAppointmentReportBuilder.Build(
            observations,
            Day,
            GapDay,
            periodFrom,
            periodTo);

        var chart = Assert.Single(model.PreviewCharts!);
        var hour10 = chart.Datasets!.Single(d => d.Label == "10:00");
        var hour14 = chart.Datasets!.Single(d => d.Label == "14:00");
        Assert.True(ChartDatasetValues.IsMissing(hour14.Values[0]));
        Assert.True(ChartDatasetValues.IsMissing(hour10.Values[1]));
    }

    [Fact]
    public void GetActiveHourRange_uses_data_hours_only()
    {
        var observations = new List<WaitingBeforeAppointmentReportBuilder.WaitingObservation>
        {
            new(Day, 6, 5.0),
            new(Day, 10, 7.0)
        };

        var range = WaitingBeforeAppointmentReportBuilder.GetActiveHourRange(observations);

        Assert.NotNull(range);
        Assert.Equal(6, range.Value.MinHour);
        Assert.Equal(10, range.Value.MaxHour);
    }

    [Fact]
    public void BuildReport_empty_observations_has_no_rows_and_no_period_totals()
    {
        var periodFrom = Day.ToDateTime(new TimeOnly(8, 0));
        var periodTo = Day.ToDateTime(new TimeOnly(18, 0));
        IReadOnlyList<WaitingBeforeAppointmentReportBuilder.WaitingObservation> observations = [];

        foreach (var purpose in new[] { ReportGenerationPurpose.ExportOrFull, ReportGenerationPurpose.JsonPreview })
        {
            var model = WaitingBeforeAppointmentReportBuilder.BuildReport(
                observations,
                Day,
                Day,
                periodFrom,
                periodTo,
                purpose);

            Assert.Empty(model.Rows);
            Assert.Null(model.PreviewCharts);
            Assert.DoesNotContain(
                model.Rows,
                r => (r.RowClass ?? "").Contains("period-total", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Build_period_total_max_is_at_least_any_hourly_max()
    {
        var periodFrom = Day.ToDateTime(new TimeOnly(8, 0));
        var periodTo = Day.ToDateTime(new TimeOnly(18, 0));
        var observations = new List<WaitingBeforeAppointmentReportBuilder.WaitingObservation>
        {
            new(Day, 10, 12.0),
            new(Day, 10, 18.0),
            new(Day, 12, 8.0)
        };

        var model = WaitingBeforeAppointmentReportBuilder.BuildReport(
            observations,
            Day,
            Day,
            periodFrom,
            periodTo,
            ReportGenerationPurpose.ExportOrFull);

        var periodRow = model.Rows.Last(r => r.RowClass == "report-load-table__row--period-total");
        var hourlyMax = observations.Max(o => o.WaitMin);
        var periodMax = double.Parse(periodRow.Cells![5], System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(periodMax >= hourlyMax);
    }
}
