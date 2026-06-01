using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class CatalogReportSharedTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(0.00004, "0")]
    [InlineData(0.0006, "0.0006")]
    [InlineData(0.006, "0.006")]
    [InlineData(0.02, "0.02")]
    [InlineData(13, "13")]
    [InlineData(13.5, "13.5")]
    [InlineData(192.436789, "192.4368")]
    public void FormatMetric_formats_zero_and_up_to_four_decimals(double value, string expected) =>
        Assert.Equal(expected, CatalogReportShared.FormatMetric(value));

    [Fact]
    public void RoundMetric_rounds_to_four_decimal_places() =>
        Assert.Equal(3.3333, CatalogReportShared.RoundMetric(10.0 / 3.0));
}
