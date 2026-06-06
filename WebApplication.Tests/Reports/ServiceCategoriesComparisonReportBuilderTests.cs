using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ServiceCategoriesComparisonReportBuilderTests
{
    [Fact]
    public void BuildReport_empty_has_eleven_headers_no_rows()
    {
        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            [],
            ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(11, model.ColumnHeaders.Count);
        Assert.Equal("Обслужено пациентов", model.ColumnHeaders[1]);
        Assert.Empty(model.Rows);
        Assert.Null(model.PreviewCharts);
    }

    [Fact]
    public void BuildReport_min_max_per_category_without_period_totals_row()
    {
        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>
        {
            new(1, 1, "ОМС", 5.0, 5.0, 10.0, 10.0),
            new(2, 1, "ОМС", 3.0, 3.0, 8.0, 8.0),
            new(2, 1, "ОМС", null, null, null, null),
            new(3, 2, "Платные", 2.0, 2.0, 15.0, 15.0),
            new(4, 2, "Платные", 1.0, 1.0, 20.0, 20.0),
            new(4, 2, "Платные", 2.0, 2.0, 25.0, 25.0),
            new(4, 2, "Платные", null, null, null, null)
        };

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            observations,
            ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(2, model.Rows.Count);

        var oms = model.Rows[0].Cells!;
        Assert.Equal("ОМС", oms[0]);
        Assert.Equal("2", oms[1]);
        Assert.Equal("4 мин", oms[2]);
        Assert.Equal("3 мин", oms[3]);
        Assert.Equal("5 мин", oms[4]);
        Assert.Equal("9 мин", oms[8]);
        Assert.Equal("8 мин", oms[9]);
        Assert.Equal("10 мин", oms[10]);

        var paid = model.Rows[1].Cells!;
        Assert.Equal("Платные", paid[0]);
        Assert.Equal("2", paid[1]);
        Assert.Equal("30 мин", paid[8]);
        Assert.Equal("15 мин", paid[9]);
        Assert.Equal("45 мин", paid[10]);
    }

    [Fact]
    public void BuildReport_horizontal_grouped_bar_matches_table_averages()
    {
        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>
        {
            new(1, 1, "ОМС", 5.0, 5.0, 10.0, 10.0),
            new(2, 1, "ОМС", 3.0, 3.0, 8.0, 8.0),
            new(3, 2, "Платные", 2.0, 2.0, 15.0, 15.0),
            new(4, 2, "Платные", 1.0, 1.0, 20.0, 20.0)
        };

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            observations,
            ReportGenerationPurpose.JsonPreview);

        Assert.NotNull(model.PreviewCharts);
        var chart = model.PreviewCharts![0];
        Assert.Equal("horizontalGroupedBar", chart.Kind);
        Assert.Equal(2, chart.Labels.Count);
        Assert.Equal("Платные", chart.Labels[0]);
        Assert.Equal("ОМС", chart.Labels[1]);
        Assert.Equal(3, chart.Datasets!.Count);
        Assert.Equal("Среднее суммарное обслуживание", chart.Datasets[0].Label);
        Assert.Equal(17.5, chart.Datasets[0].Values[0]);
        Assert.Equal(9, chart.Datasets[0].Values[1]);
        Assert.Equal(1.5, chart.Datasets[1].Values[0]);
        Assert.Equal(4, chart.Datasets[1].Values[1]);
        Assert.Equal(17.5, chart.Datasets[2].Values[0]);
        Assert.Equal(9, chart.Datasets[2].Values[1]);
        Assert.Equal("мин", chart.ValueUnit);
    }

    [Fact]
    public void BuildReport_horizontal_chart_null_when_no_finite_metrics()
    {
        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>
        {
            new(1, 1, "A", null, null, null, null),
            new(2, 1, "A", null, null, null, null)
        };

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            observations,
            ReportGenerationPurpose.JsonPreview);

        Assert.Null(model.PreviewCharts);
    }

    [Fact]
    public void BuildReport_horizontal_chart_wait_only_leaves_svc_missing()
    {
        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>
        {
            new(1, 1, "Только ожидание", 10.0, 10.0, null, null)
        };

        var model = ServiceCategoriesComparisonReportBuilder.BuildReport(
            observations,
            ReportGenerationPurpose.JsonPreview);

        Assert.NotNull(model.PreviewCharts);
        var chart = model.PreviewCharts![0];
        Assert.True(ChartDatasetValues.IsMissing(chart.Datasets![0].Values[0]));
        Assert.Equal(10, chart.Datasets[1].Values[0]);
        Assert.True(ChartDatasetValues.IsMissing(chart.Datasets[2].Values[0]));
    }
}
