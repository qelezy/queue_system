using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class AppointmentDurationReportBuilderTests
{
    private static readonly DateOnly Day1 = new(2026, 5, 10);
    private static readonly DateOnly EmptyDay = new(2026, 5, 11);
    private static readonly DateOnly Day2 = new(2026, 5, 12);

    [Fact]
    public void BuildReport_empty_observations_has_no_rows()
    {
        foreach (var purpose in new[] { ReportGenerationPurpose.ExportOrFull, ReportGenerationPurpose.JsonPreview })
        {
            var model = AppointmentDurationReportBuilder.BuildReport(
                [],
                Day1,
                Day1,
                AppointmentDurationReportBuilder.ModeDoctor,
                purpose);

            Assert.Empty(model.Rows);
            Assert.Null(model.PreviewCharts);
            Assert.DoesNotContain(
                model.Rows,
                r => (r.RowClass ?? "").Contains("period-total", StringComparison.Ordinal)
                    || (r.RowClass ?? "").Contains("totals-start", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Build_skips_empty_day_in_table_and_chart()
    {
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, 20.0, 15, "Therapy"),
            new(Day2, "Dr A", 2, 18.0, 15, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day2,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var detailRows = model.Rows
            .Where(r => string.IsNullOrWhiteSpace(r.RowClass))
            .ToList();
        Assert.Equal(2, detailRows.Count);
        Assert.DoesNotContain(detailRows, r => (r.Cells?[0] ?? "").Contains("2026-05-11"));

        var chart = Assert.Single(model.PreviewCharts!);
        Assert.Equal(2, chart.Labels.Count);
    }

    [Fact]
    public void Build_chart_uses_missing_when_slice_inactive_on_day()
    {
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, 20.0, 15, "Therapy"),
            new(Day1, "Dr B", 2, 22.0, 15, "Therapy"),
            new(Day2, "Dr A", 3, 18.0, 15, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day2,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var chart = Assert.Single(model.PreviewCharts!);
        var drB = chart.Datasets!.Single(d => d.Label == "Dr B");
        Assert.False(ChartDatasetValues.IsMissing(drB.Values[0]));
        Assert.True(ChartDatasetValues.IsMissing(drB.Values[1]));
        Assert.True(ChartDatasetValues.IsMissing(drB.NormValues![1]));
    }

    [Fact]
    public void BuildReport_multi_stage_ticket_avg_differs_from_max()
    {
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, 10.0, 15, "Therapy"),
            new(Day1, "Dr A", 1, 10.0, 15, "Therapy"),
            new(Day1, "Dr A", 1, 60.0, 15, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var detailRow = model.Rows.First(r => string.IsNullOrWhiteSpace(r.RowClass));
        var avg = double.Parse(detailRow.Cells![4], System.Globalization.CultureInfo.InvariantCulture);
        var max = double.Parse(detailRow.Cells[8], System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(26.7, avg, 1);
        Assert.Equal(60, max);
        Assert.True(avg < max);
    }
}
