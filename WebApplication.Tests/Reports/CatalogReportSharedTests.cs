using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class CatalogReportSharedTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(45.4, 45)]
    [InlineData(45.6, 46)]
    [InlineData(-90.4, -90)]
    public void RoundDurationMinutes_rounds_away_from_zero(double value, int expected) =>
        Assert.Equal(expected, CatalogReportShared.RoundDurationMinutes(value));

    [Theory]
    [InlineData(0, "0 с")]
    [InlineData(0.4, "24 с")]
    [InlineData(20.0 / 60.0, "20 с")]
    [InlineData(45.0 / 60.0, "45 с")]
    [InlineData(90.0 / 60.0, "2 мин")]
    [InlineData(0.5, "30 с")]
    [InlineData(0.0001, "0 с")]
    [InlineData(-0.25, "-15 с")]
    [InlineData(45.6, "46 мин")]
    [InlineData(125, "2 ч 5 мин")]
    [InlineData(120, "2 ч")]
    [InlineData(-90, "-1 ч 30 мин")]
    [InlineData(59, "59 мин")]
    public void FormatDuration_humanizes_minutes(double value, string expected) =>
        Assert.Equal(expected, CatalogReportShared.FormatDuration(value));

    [Fact]
    public void ComputeDurationMinutes_rounds_total_seconds()
    {
        var start = new DateTime(2026, 5, 18, 10, 0, 0);
        var end = start.AddSeconds(20);
        var minutes = CatalogReportShared.ComputeDurationMinutes(start, end);
        Assert.NotNull(minutes);
        Assert.Equal("20 с", CatalogReportShared.FormatDuration(minutes.Value));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(33.333, "33.3")]
    [InlineData(12.04, "12")]
    [InlineData(99.95, "100")]
    public void FormatPercent_one_decimal_place(double value, string expected) =>
        Assert.Equal(expected, CatalogReportShared.FormatPercent(value));

    [Fact]
    public void FormatMultiStageSharePercent_uses_percent_format() =>
        Assert.Equal("33.3", CatalogReportShared.FormatMultiStageSharePercent(2, 1));

    [Theory]
    [InlineData(-4.67, -5)]
    [InlineData(4.67, 5)]
    [InlineData(90.0 / 60.0, 2)]
    public void RoundDurationDisplayChartValue_matches_FormatDuration_minute_granularity(
        double minutes,
        double expected) =>
        Assert.Equal(expected, CatalogReportShared.RoundDurationDisplayChartValue(minutes));

    [Fact]
    public void RoundDurationDisplayChartValue_minus_4_67_formats_as_minus_5_min()
    {
        Assert.Equal("5 мин", CatalogReportShared.FormatDuration(4.67));
        Assert.Equal(-5, CatalogReportShared.RoundDurationDisplayChartValue(-4.67));
    }

    [Fact]
    public void ComputeDurationMinutesExact_uses_fractional_total_minutes()
    {
        var start = new DateTime(2026, 5, 10, 10, 0, 0);
        var end = start.AddSeconds(49.5);
        var minutes = CatalogReportShared.ComputeDurationMinutesExact(start, end);

        Assert.NotNull(minutes);
        Assert.Equal(0.83, Math.Round(minutes.Value, 2, MidpointRounding.AwayFromZero), 2);
    }

    [Theory]
    [InlineData("49 с", 0.82)]
    [InlineData("4 мин", 4.0)]
    [InlineData("1 ч 30 мин", 90.0)]
    [InlineData("—", null)]
    public void TryParseFormattedDurationToMinutes_parses_report_cells(string cell, double? expectedMinutes)
    {
        var ok = CatalogReportShared.TryParseFormattedDurationToMinutes(cell, out var minutes);
        if (expectedMinutes is null)
        {
            Assert.False(ok);
            return;
        }

        Assert.True(ok);
        Assert.Equal(expectedMinutes.Value, Math.Round(minutes, 2, MidpointRounding.AwayFromZero), 2);
    }

    [Theory]
    [InlineData(0.8166666666666667, "0.82")]
    [InlineData(4, "4.00")]
    [InlineData(90, "90.00")]
    public void FormatMinutesForCsv_rounds_to_two_decimals(double minutes, string expected) =>
        Assert.Equal(expected, CatalogReportShared.FormatMinutesForCsv(minutes));

    [Fact]
    public void AverageDurationMinutes_uses_second_precision()
    {
        var values = new List<double> { 20.0 / 60.0, 40.0 / 60.0 };
        var avg = CatalogReportShared.AverageDurationMinutes(values);
        Assert.Equal("30 с", CatalogReportShared.FormatDuration(avg));
    }
}
