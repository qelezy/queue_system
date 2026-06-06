using WebApplication.Models.Reports.Charts;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class HorizontalGroupedBarChartMetricsTests
{
    [Fact]
    public void BuildAxisTickValues_uses_even_minute_steps()
    {
        var ticks = HorizontalGroupedBarChartMetrics.BuildAxisTickValues(19);

        Assert.Equal([0, 5, 10, 15, 19], ticks);
    }

    [Fact]
    public void ResolveSplitAxisBounds_uses_separate_positive_and_negative_extents()
    {
        var (axisMin, axisMax) = HorizontalGroupedBarChartMetrics.ResolveSplitAxisBounds(
            [10.0, 5.0, -4.5, double.NaN]);

        Assert.Equal(-5, axisMin);
        Assert.Equal(10, axisMax);
    }

    [Fact]
    public void ResolveSplitAxisBounds_covers_display_rounded_negative_deviation()
    {
        var (axisMin, _) = HorizontalGroupedBarChartMetrics.ResolveSplitAxisBounds([-4.67]);

        Assert.True(axisMin <= -5);
    }

    [Fact]
    public void BuildBidirectionalAxisTickValues_spans_axisMin_and_axisMax()
    {
        var ticks = HorizontalGroupedBarChartMetrics.BuildBidirectionalAxisTickValues(-5, 10);

        Assert.Equal(-5, ticks[0]);
        Assert.Contains(0, ticks);
        Assert.Equal(10, ticks[^1]);
    }
}
