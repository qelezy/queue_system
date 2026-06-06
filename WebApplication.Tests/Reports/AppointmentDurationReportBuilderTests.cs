using System.Text;
using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.Catalog;
using Xunit;

using static WebApplication.Services.Reports.Catalog.CatalogReportShared;

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
            new(Day1, "Dr A", 1, 20.0, 20.0, 15, "Therapy"),
            new(Day2, "Dr A", 2, 18.0, 18.0, 15, "Therapy")
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
        Assert.Equal("horizontalGroupedBar", chart.Kind);
        Assert.Single(chart.Labels);
        Assert.Equal("Dr A", chart.Labels[0]);
    }

    [Fact]
    public void Build_period_horizontal_chart_all_slices_three_series_signed_deviation()
    {
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, 5.0, 5.0, 15, "Therapy"),
            new(Day1, "Dr B", 2, 20.0, 20.0, 15, "Therapy"),
            new(Day2, "Dr A", 3, 18.0, 18.0, 15, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day2,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var chart = Assert.Single(model.PreviewCharts!);
        Assert.Equal("horizontalGroupedBar", chart.Kind);
        Assert.Equal("symmetric", chart.ChartAxisMode);
        Assert.Equal(2, chart.Labels.Count);
        Assert.Equal(["Dr B", "Dr A"], chart.Labels);
        Assert.Equal(3, chart.Datasets!.Count);

        var deviation = chart.Datasets.Single(d => d.Label == "Отклонение");
        Assert.True(deviation.Values[1] < 0);
        Assert.True(deviation.Values[0] > 0);
        Assert.True(ChartDatasetValues.IsMissing(
            chart.Datasets.Single(d => d.Label == "Средняя длительность приёма").Values[0]) == false);
    }

    [Fact]
    public void Build_period_chart_slices_ordered_by_average_descending()
    {
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, 5.0, 5.0, 15, "Therapy"),
            new(Day1, "Dr B", 2, 20.0, 20.0, 15, "Therapy"),
            new(Day1, "Dr C", 3, 12.0, 12.0, 15, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var chart = Assert.Single(model.PreviewCharts!);
        var avg = chart.Datasets!.Single(d => d.Label == "Средняя длительность приёма").Values;

        Assert.Equal(["Dr B", "Dr C", "Dr A"], chart.Labels);
        Assert.True(avg[0] >= avg[1]);
        Assert.True(avg[1] >= avg[2]);
    }

    [Fact]
    public void Build_period_chart_all_series_match_display_minute_values()
    {
        const double svcMin = 10.0 + 20.0 / 60.0;
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, svcMin, svcMin, 15, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var chart = Assert.Single(model.PreviewCharts!);
        Assert.NotNull(chart.Datasets);
        var datasets = chart.Datasets;
        var avgRaw = AverageDurationMinutes([svcMin]);
        var normRaw = 15.0;
        var deviationRaw = avgRaw - normRaw;

        var avg = datasets.Single(d => d.Label == "Средняя длительность приёма").Values[0];
        var norm = datasets.Single(d => d.Label == "Норматив").Values[0];
        var deviation = datasets.Single(d => d.Label == "Отклонение").Values[0];

        Assert.Equal(RoundDurationDisplayChartValue(avgRaw), avg);
        Assert.Equal(RoundDurationDisplayChartValue(normRaw), norm);
        Assert.Equal(RoundDurationDisplayChartValue(deviationRaw), deviation);
        Assert.Equal("10 мин", FormatDuration(avgRaw));
        Assert.Equal("15 мин", FormatDuration(normRaw));
        Assert.Equal("5 мин", FormatDuration(Math.Abs(deviationRaw)));
    }

    [Fact]
    public void BuildReport_multi_stage_ticket_avg_differs_from_max()
    {
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, 10.0, 10.0, 15, "Therapy"),
            new(Day1, "Dr A", 1, 10.0, 10.0, 15, "Therapy"),
            new(Day1, "Dr A", 1, 60.0, 60.0, 15, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var detailRow = model.Rows.First(r => string.IsNullOrWhiteSpace(r.RowClass));

        ReportsDurationTestHelper.AssertDurationCell(27, detailRow.Cells![4]);
        ReportsDurationTestHelper.AssertDurationCell(60, detailRow.Cells[9]);
        Assert.True(
            ReportsDurationTestHelper.ParseDurationCell(detailRow.Cells[4])
            < ReportsDurationTestHelper.ParseDurationCell(detailRow.Cells[9]));
    }

    [Fact]
    public void Build_period_chart_deviation_matches_period_totals_not_rounded_avg_minus_norm()
    {
        const double svcMin = 2.5;
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, svcMin, svcMin, 4, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var periodRow = model.Rows.Single(r =>
            (r.RowClass ?? "").Contains("period-total", StringComparison.Ordinal));
        var chart = Assert.Single(model.PreviewCharts!);
        var datasets = chart.Datasets!;
        var avgRaw = AverageDurationMinutes([svcMin]);
        var deviationRaw = avgRaw - 4.0;

        Assert.Equal(FormatDuration(avgRaw), periodRow.Cells![4]);
        Assert.Equal("4 мин", periodRow.Cells[5]);
        Assert.Equal(FormatDuration(Math.Abs(deviationRaw)), periodRow.Cells[6]);
        Assert.Equal("—", periodRow.Cells[7]);

        Assert.Equal(RoundDurationDisplayChartValue(avgRaw),
            datasets.Single(d => d.Label == "Средняя длительность приёма").Values[0]);
        Assert.Equal(4,
            datasets.Single(d => d.Label == "Норматив").Values[0]);
        Assert.Equal(RoundDurationDisplayChartValue(deviationRaw),
            datasets.Single(d => d.Label == "Отклонение").Values[0]);
        Assert.NotEqual(
            RoundDurationDisplayChartValue(avgRaw) - RoundDurationDisplayChartValue(4),
            datasets.Single(d => d.Label == "Отклонение").Values[0]);
    }

    [Fact]
    public void BuildReport_detail_csv_cells_use_exact_minutes()
    {
        const double svcMinExact = 10.0 + 49.5 / 60.0;
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, svcMinExact, svcMinExact, 15, "Therapy")
        };

        var model = AppointmentDurationReportBuilder.BuildReport(
            observations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);

        var detailRow = model.Rows.First(r => string.IsNullOrWhiteSpace(r.RowClass));
        Assert.NotNull(detailRow.CsvCells);
        var csv = Encoding.UTF8.GetString(ReportTabularExporter.WriteCsvBytes(model));
        Assert.Contains(FormatMinutesForCsv(svcMinExact), csv);
    }

    [Fact]
    public void BuildReport_deviation_splits_into_faster_and_slower_columns()
    {
        var fasterObservations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr A", 1, 5.0, 5.0, 15, "Therapy")
        };
        var fasterModel = AppointmentDurationReportBuilder.BuildReport(
            fasterObservations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);
        var fasterRow = fasterModel.Rows.First(r => string.IsNullOrWhiteSpace(r.RowClass));
        Assert.Equal("—", fasterRow.Cells![7]);
        ReportsDurationTestHelper.AssertDurationCell(10, fasterRow.Cells[6]);
        Assert.DoesNotContain("-", fasterRow.Cells[6]!, StringComparison.Ordinal);

        var slowerObservations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr B", 2, 20.0, 20.0, 15, "Therapy")
        };
        var slowerModel = AppointmentDurationReportBuilder.BuildReport(
            slowerObservations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);
        var slowerRow = slowerModel.Rows.First(r => string.IsNullOrWhiteSpace(r.RowClass));
        Assert.Equal("—", slowerRow.Cells![6]);
        ReportsDurationTestHelper.AssertDurationCell(5, slowerRow.Cells[7]);

        var onNormObservations = new List<AppointmentDurationReportBuilder.DurationObservation>
        {
            new(Day1, "Dr C", 3, 15.0, 15.0, 15, "Therapy")
        };
        var onNormModel = AppointmentDurationReportBuilder.BuildReport(
            onNormObservations,
            Day1,
            Day1,
            AppointmentDurationReportBuilder.ModeDoctor,
            ReportGenerationPurpose.ExportOrFull);
        var onNormRow = onNormModel.Rows.First(r => string.IsNullOrWhiteSpace(r.RowClass));
        Assert.Equal("—", onNormRow.Cells![6]);
        Assert.Equal("—", onNormRow.Cells[7]);

        Assert.Contains(fasterModel.ColumnHeaders, h => h.Contains("Работает быстрее", StringComparison.Ordinal));
        Assert.Contains(fasterModel.ColumnHeaders, h => h.Contains("Работает медленнее", StringComparison.Ordinal));
        Assert.Contains("Самый короткий приём", fasterModel.ColumnHeaders);
        Assert.Contains("Самый длинный приём", fasterModel.ColumnHeaders);
    }
}
