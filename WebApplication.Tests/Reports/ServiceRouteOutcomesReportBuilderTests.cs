using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ServiceRouteOutcomesReportBuilderTests
{
    private static readonly DateOnly Day = new(2026, 5, 10);

    private static readonly Dictionary<int, (string Name, int Priority)> Categories =
        new() { [1] = ("Категория А", 1) };

    [Fact]
    public void ColumnHeaders_has_five_columns_without_no_shows()
    {
        Assert.Equal(5, ServiceRouteOutcomesReportBuilder.ColumnHeaders.Length);
        Assert.Equal("Обращений", ServiceRouteOutcomesReportBuilder.ColumnHeaders[2]);
        Assert.Equal("Полностью обслужено", ServiceRouteOutcomesReportBuilder.ColumnHeaders[3]);
        Assert.Equal("С незавершённым обслуживанием", ServiceRouteOutcomesReportBuilder.ColumnHeaders[4]);
    }

    [Fact]
    public void BuildReport_aggregates_day_and_category()
    {
        var appointments = new List<CatalogAppointmentObservations.AppointmentObservation>
        {
            new(1, Day, 1),
            new(2, Day, 1)
        };
        var listItems = new List<CatalogAppointmentObservations.ListItemObservation>
        {
            new(2, new TimeOnly(9, 0), new TimeOnly(9, 10), null)
        };

        var model = ServiceRouteOutcomesReportBuilder.BuildReport(
            appointments, listItems, Categories, ReportGenerationPurpose.ExportOrFull);

        Assert.Single(model.Rows, r => (r.RowClass ?? "").Contains("period-total"));
        var detail = model.Rows.Where(r => string.IsNullOrWhiteSpace(r.RowClass)).ToList();
        Assert.Single(detail);
        Assert.Equal("2", detail[0].Cells[2]);
        Assert.Equal("0", detail[0].Cells[3]);
        Assert.Equal("1", detail[0].Cells[4]);
    }

    [Fact]
    public void BuildReport_totals_sum_completed_and_incomplete()
    {
        var appointments = new List<CatalogAppointmentObservations.AppointmentObservation>
        {
            new(1, Day, 1),
            new(2, Day, 1),
            new(3, Day, 1),
            new(4, Day, 1)
        };
        var listItems = new List<CatalogAppointmentObservations.ListItemObservation>
        {
            new(3, new TimeOnly(9, 0), new TimeOnly(9, 5), null),
            new(4, new TimeOnly(10, 0), new TimeOnly(10, 5), new TimeOnly(10, 30))
        };

        var model = ServiceRouteOutcomesReportBuilder.BuildReport(
            appointments, listItems, Categories, ReportGenerationPurpose.ExportOrFull);

        var total = model.Rows.First(r => (r.RowClass ?? "").Contains("period-total"));
        Assert.Equal("4", total.Cells[2]);
        Assert.Equal("1", total.Cells[3]);
        Assert.Equal("1", total.Cells[4]);
    }

    [Fact]
    public void BuildReport_chart_skips_calendar_days_without_data()
    {
        var day1 = new DateOnly(2026, 5, 10);
        var day3 = new DateOnly(2026, 5, 12);
        var appointments = new List<CatalogAppointmentObservations.AppointmentObservation>
        {
            new(1, day1, 1),
            new(2, day3, 1)
        };
        var listItems = new List<CatalogAppointmentObservations.ListItemObservation>
        {
            new(1, new TimeOnly(9, 0), new TimeOnly(9, 5), new TimeOnly(9, 30)),
            new(2, new TimeOnly(10, 0), new TimeOnly(10, 5), new TimeOnly(10, 30))
        };

        var model = ServiceRouteOutcomesReportBuilder.BuildReport(
            appointments, listItems, Categories, ReportGenerationPurpose.ExportOrFull);

        var bar = Assert.Single(model.PreviewCharts!);
        Assert.Equal("groupedBar", bar.Kind);
        Assert.Equal("stacked", bar.ChartAxisMode);
        Assert.Equal(2, bar.Labels.Count);
        Assert.DoesNotContain(bar.Labels, l => l.Contains("11-05-2026", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildReport_appointment_without_list_items_counts_only_in_tickets()
    {
        var appointments = new List<CatalogAppointmentObservations.AppointmentObservation>
        {
            new(1, Day, 1)
        };

        var model = ServiceRouteOutcomesReportBuilder.BuildReport(
            appointments, [], Categories, ReportGenerationPurpose.ExportOrFull);

        var detail = model.Rows.First(r => string.IsNullOrWhiteSpace(r.RowClass));
        Assert.Equal("1", detail.Cells[2]);
        Assert.Equal("0", detail.Cells[3]);
        Assert.Equal("0", detail.Cells[4]);
    }
}
