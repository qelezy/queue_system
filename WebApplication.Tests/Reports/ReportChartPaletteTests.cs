using WebApplication.Services.Reports;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ReportChartPaletteTests
{
    [Fact]
    public void Fill_all_twenty_four_indices_are_unique()
    {
        var fills = Enumerable.Range(0, ReportChartPalette.SeriesColorCount)
            .Select(ReportChartPalette.Fill)
            .ToList();

        Assert.Equal(ReportChartPalette.SeriesColorCount, fills.Distinct().Count());
    }

    [Fact]
    public void Fill_zero_is_legacy_teal()
    {
        Assert.Equal("rgba(0,179,184,0.88)", ReportChartPalette.Fill(0));
    }

    [Fact]
    public void Fill_spans_at_least_eight_hue_families()
    {
        var hues = Enumerable.Range(0, ReportChartPalette.SeriesColorCount)
            .Select(i =>
            {
                var rgba = ReportChartPalette.Fill(i);
                var inner = rgba[5..^1];
                var parts = inner.Split(',');
                var r = int.Parse(parts[0]) / 255.0;
                var g = int.Parse(parts[1]) / 255.0;
                var b = int.Parse(parts[2]) / 255.0;
                return RgbToHueBucket(r, g, b);
            })
            .Distinct()
            .Count();

        Assert.True(hues >= 8, $"Expected at least 8 hue families, got {hues}.");
    }

    private static int RgbToHueBucket(double r, double g, double b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        if (max - min < 0.08)
            return 0;

        var hue = 0.0;
        if (max == r)
            hue = (60 * ((g - b) / (max - min)) + 360) % 360;
        else if (max == g)
            hue = 60 * ((b - r) / (max - min)) + 120;
        else
            hue = 60 * ((r - g) / (max - min)) + 240;

        return (int)(hue / 24);
    }
}
