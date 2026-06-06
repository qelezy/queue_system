using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

internal static class ReportsDurationTestHelper
{
    internal static int ParseDurationCell(string? cell)
    {
        if (!CatalogReportShared.TryParseFormattedDurationToMinutes(cell, out var minutes))
            throw new FormatException($"Unrecognized duration cell: '{cell}'.");

        return (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
    }

    internal static void AssertDurationCell(double expectedMinutes, string? actualCell, double toleranceMinutes = 0.5)
    {
        var trimmed = actualCell?.Trim();
        if (trimmed is not null
            && CatalogReportShared.TryParseFormattedDurationToMinutes(trimmed, out var parsed)
            && trimmed.EndsWith(" с", StringComparison.Ordinal)
            && !trimmed.Contains("мин", StringComparison.Ordinal)
            && !trimmed.Contains('ч'))
        {
            var expectedSeconds = (int)Math.Round(expectedMinutes * 60.0, MidpointRounding.AwayFromZero);
            var actualSeconds = (int)Math.Round(parsed * 60.0, MidpointRounding.AwayFromZero);
            Assert.Equal(expectedSeconds, actualSeconds);
            return;
        }

        var actual = ParseDurationCell(actualCell);
        Assert.True(
            Math.Abs(expectedMinutes - actual) <= toleranceMinutes,
            $"expected {expectedMinutes} min, actual '{actualCell}' ({actual} min)");
    }
}
