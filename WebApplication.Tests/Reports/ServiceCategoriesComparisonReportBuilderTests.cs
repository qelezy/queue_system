using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ServiceCategoriesComparisonReportBuilderTests
{
    [Fact]
    public void BuildReport_empty_has_eight_headers_no_rows()
    {
        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            [],
            ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(8, model.ColumnHeaders.Count);
        Assert.Equal("Приёмов", model.ColumnHeaders[1]);
        Assert.Empty(model.Rows);
        Assert.Null(model.PreviewCharts);
    }

    [Fact]
    public void BuildReport_min_max_per_category_without_period_totals_row()
    {
        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>
        {
            new(1, 1, "ОМС", 5.0, 10.0),
            new(2, 1, "ОМС", 3.0, 8.0),
            new(2, 1, "ОМС", null, null),
            new(3, 2, "Платные", 2.0, 15.0),
            new(4, 2, "Платные", 1.0, 20.0),
            new(4, 2, "Платные", 2.0, 25.0),
            new(4, 2, "Платные", null, null)
        };

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            observations,
            ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(2, model.Rows.Count);

        var oms = model.Rows[0].Cells!;
        Assert.Equal("ОМС", oms[0]);
        Assert.Equal("2", oms[1]);
        Assert.Equal("4", oms[2]);
        Assert.Equal("3", oms[3]);
        Assert.Equal("5", oms[4]);

        var paid = model.Rows[1].Cells!;
        Assert.Equal("Платные", paid[0]);
        Assert.Equal("2", paid[1]);
    }

    [Fact]
    public void BuildReport_doughnut_classifies_routes_by_list_item_count()
    {
        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>
        {
            new(1, 1, "A", null, null),
            new(2, 1, "A", null, null),
            new(2, 1, "A", null, null)
        };

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            observations,
            ReportGenerationPurpose.JsonPreview);

        Assert.NotNull(model.PreviewCharts);
        var chart = model.PreviewCharts![0];
        Assert.Equal(2, chart.Labels.Count);
        Assert.Equal("Одноэтапные маршруты", chart.Labels[0]);
        Assert.Equal("Многоэтапные маршруты", chart.Labels[1]);
        Assert.Equal(1, chart.Values[0]);
        Assert.Equal(1, chart.Values[1]);
        Assert.Equal("маршрутов", chart.ValueUnit);
    }

    [Fact]
    public void BuildReport_doughnut_counts_all_c1_and_cge2_regardless_of_category()
    {
        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>
        {
            new(1, 1, "A", null, null),
            new(2, 2, "B", null, null),
            new(2, 2, "B", null, null),
            new(2, 2, "B", null, null)
        };

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            observations,
            ReportGenerationPurpose.JsonPreview);

        Assert.NotNull(model.PreviewCharts);
        Assert.Equal(1, model.PreviewCharts![0].Values[0]);
        Assert.Equal(1, model.PreviewCharts[0].Values[1]);
    }
}
