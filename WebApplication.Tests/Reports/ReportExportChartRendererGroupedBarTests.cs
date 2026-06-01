using System.Text.RegularExpressions;
using WebApplication.Services.Reports;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ReportExportChartRendererGroupedBarTests
{
    [Fact]
    public void RenderChartSvgs_three_series_use_distinct_palette_colors_and_full_legend()
    {
        var descriptor = BuildGroupedBarDescriptor(3, i => $"Серия {i + 1}");

        var svgs = ReportExportChartRenderer.RenderChartSvgs(new ReportResultViewModel
        {
            PreviewCharts = [descriptor]
        });

        Assert.Single(svgs);
        var svg = svgs[0];
        Assert.Contains(ReportChartPalette.Fill(0), svg);
        Assert.Contains(ReportChartPalette.Fill(1), svg);
        Assert.Contains("Серия 1", svg);
        Assert.Contains("Серия 2", svg);
        Assert.Contains("Серия 3", svg);
        Assert.DoesNotContain("Серии различаются подписями на столбцах", svg);
    }

    [Fact]
    public void RenderChartSvgs_twenty_four_series_use_many_distinct_colors_and_full_legend()
    {
        var descriptor = BuildGroupedBarDescriptor(24, i => $"H{i}");

        var svg = ReportExportChartRenderer.RenderChartSvgs(new ReportResultViewModel
        {
            PreviewCharts = [descriptor]
        })[0];

        var fills = Regex.Matches(svg, @"fill=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Where(f => f.StartsWith("rgba(", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        Assert.True(fills.Count >= 20, $"Expected at least 20 distinct bar fills, got {fills.Count}.");

        for (var i = 0; i < 24; i++)
            Assert.Contains($"H{i}", svg);

        Assert.DoesNotContain("Серии различаются подписями на столбцах", svg);
        Assert.DoesNotContain("• H0", svg);
    }

    [Fact]
    public void RenderChartSvgs_legend_is_horizontally_centered_for_many_series()
    {
        var descriptor = BuildGroupedBarDescriptor(24, i => $"H{i}");

        var svg = ReportExportChartRenderer.RenderChartSvgs(new ReportResultViewModel
        {
            PreviewCharts = [descriptor]
        })[0];

        const double padL = 52;
        const double padR = 20;
        var viewBoxMatch = Regex.Match(svg, @"viewBox=""0 0 ([\d.]+) ([\d.]+)""");
        Assert.True(viewBoxMatch.Success);
        var canvasW = double.Parse(viewBoxMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var plotW = canvasW - padL - padR;
        var plotCenter = padL + plotW / 2;

        const double legendColWidth = 120;
        var legendCols = Math.Max(1, (int)Math.Floor(plotW / legendColWidth));
        var legendBlockW = legendCols * legendColWidth;
        var expectedLegendCenter = padL + (plotW - legendBlockW) / 2 + legendBlockW / 2;

        Assert.InRange(Math.Abs(expectedLegendCenter - plotCenter), 0, 0.01);

        var legendSwatchXs = Regex.Matches(svg, @"<rect x=""([\d.]+)"" y=""([\d.]+)"" width=""10"" height=""10""")
            .Select(m => (
                X: double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                Y: double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .Where(p => p.Y > 320)
            .Select(p => p.X)
            .ToList();

        Assert.NotEmpty(legendSwatchXs);
        var legendLeft = legendSwatchXs.Min();
        var legendRight = legendSwatchXs.Max() + legendColWidth;
        var legendCenter = (legendLeft + legendRight) / 2;
        Assert.InRange(Math.Abs(legendCenter - plotCenter), 0, 20);
    }

    [Fact]
    public void RenderChartSvgs_grouped_bar_height_does_not_exceed_cap_with_many_labels()
    {
        var descriptor = new ReportPreviewChartDescriptor
        {
            Kind = "groupedBar",
            Labels = Enumerable.Range(1, 30).Select(i => $"day{i}").ToList(),
            ValueUnit = "мин",
            Datasets =
            [
                new ReportPreviewChartDataset
                {
                    Label = "Series",
                    Values = Enumerable.Repeat(10.0, 30).ToList()
                }
            ]
        };

        var svg = ReportExportChartRenderer.RenderChartSvgs(new ReportResultViewModel
        {
            PreviewCharts = [descriptor]
        })[0];

        var heightMatch = Regex.Match(svg, @"height=""([\d.]+)""");
        Assert.True(heightMatch.Success);
        var height = double.Parse(heightMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(height <= 420.01, $"Expected capped SVG height, got {height}.");
    }

    private static ReportPreviewChartDescriptor BuildGroupedBarDescriptor(
        int seriesCount,
        Func<int, string> labelFactory) =>
        new()
        {
            Kind = "groupedBar",
            Labels = ["01-05-2026", "02-05-2026"],
            ValueUnit = "мин",
            Datasets = Enumerable.Range(0, seriesCount)
                .Select(i => new ReportPreviewChartDataset
                {
                    Label = labelFactory(i),
                    Values = [10 + i, 5]
                })
                .ToList()
        };
}
