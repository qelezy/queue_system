using System.Globalization;
using WebApplication.Services.Demo;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class RouteAndPausesReportBuilderTests
{
    private static readonly DateOnly Day = new(2026, 5, 10);
    private static readonly DateTime PeriodStart = new(2026, 5, 10, 10, 0, 0);
    private static readonly DateTime PeriodEnd = new(2026, 5, 10, 12, 0, 0);
    private static readonly TimeOnly DefaultArrival = new(8, 0);

    [Fact]
    public void FormatFullServiceInterval_uses_time_arrival()
    {
        var ordered = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(1, new TimeOnly(9, 0), new TimeOnly(9, 30), new TimeOnly(8, 50)),
            Obs(1, new TimeOnly(10, 0), new TimeOnly(10, 20))
        };

        var text = RouteAndPausesReportBuilder.FormatFullServiceInterval(
            Day, ordered, new DateTime(2026, 5, 10, 0, 0, 0), new DateTime(2026, 5, 10, 23, 59, 59));

        Assert.Equal("08:00–10:20", text);
    }

    [Fact]
    public void FormatFullServiceInterval_clips_to_period()
    {
        var ordered = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(1, new TimeOnly(9, 0), new TimeOnly(11, 0), new TimeOnly(8, 30)),
            Obs(1, new TimeOnly(11, 30), new TimeOnly(12, 30))
        };

        var text = RouteAndPausesReportBuilder.FormatFullServiceInterval(Day, ordered, PeriodStart, PeriodEnd);

        Assert.Equal("10:00–12:00", text);
    }

    [Fact]
    public void SumRouteDurationMinutes_clips_to_period()
    {
        var ordered = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(1, new TimeOnly(9, 0), new TimeOnly(11, 0)),
            Obs(1, new TimeOnly(11, 30), new TimeOnly(12, 30))
        };

        var sum = RouteAndPausesReportBuilder.SumRouteDurationMinutes(Day, ordered, PeriodStart, PeriodEnd);

        Assert.Equal(90, sum, precision: 5);
    }

    [Fact]
    public void SumPauseMinutes_clips_call_to_start_pause_to_period()
    {
        var ordered = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(1, new TimeOnly(10, 10), new TimeOnly(10, 30), new TimeOnly(9, 50)),
            Obs(1, new TimeOnly(10, 40), new TimeOnly(11, 0), new TimeOnly(10, 30))
        };

        var pause = RouteAndPausesReportBuilder.SumPauseMinutes(Day, ordered, PeriodStart, PeriodEnd);

        Assert.Equal(20, pause, precision: 5);
    }

    [Fact]
    public void BuildReport_excludes_single_stage_appointments()
    {
        var stages = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(1, new TimeOnly(8, 0), new TimeOnly(8, 30)),
            Obs(2, new TimeOnly(10, 0), new TimeOnly(10, 30)),
            Obs(2, new TimeOnly(11, 0), new TimeOnly(11, 30))
        };

        var model = RouteAndPausesReportBuilder.BuildReport(
            stages,
            new DateTime(2026, 5, 10, 0, 0, 0),
            new DateTime(2026, 5, 10, 23, 59, 59),
            ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(3, model.Rows.Count);
        Assert.Equal("2", model.Rows[0].Cells[2]);
    }

    [Fact]
    public void BuildReport_excludes_multi_stage_without_period_intersection()
    {
        var stages = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(1, new TimeOnly(7, 0), new TimeOnly(7, 30)),
            Obs(1, new TimeOnly(8, 0), new TimeOnly(8, 30))
        };

        var model = RouteAndPausesReportBuilder.BuildReport(
            stages, PeriodStart, PeriodEnd, ReportGenerationPurpose.ExportOrFull);

        Assert.Empty(model.Rows);
    }

    [Fact]
    public void BuildReport_includes_multi_stage_with_clipped_metrics()
    {
        var stages = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(1, new TimeOnly(9, 0), new TimeOnly(11, 0)),
            Obs(1, new TimeOnly(11, 30), new TimeOnly(12, 30))
        };

        var model = RouteAndPausesReportBuilder.BuildReport(
            stages, PeriodStart, PeriodEnd, ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(3, model.Rows.Count);
        Assert.Equal("2", model.Rows[0].Cells[2]);
        Assert.Equal("90", model.Rows[0].Cells[3]);
        Assert.Equal("0", model.Rows[0].Cells[4]);
    }

    [Fact]
    public void BuildReport_has_five_columns_without_patient_or_talon_wording()
    {
        var stages = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(2, new TimeOnly(10, 0), new TimeOnly(10, 30)),
            Obs(2, new TimeOnly(11, 0), new TimeOnly(11, 30))
        };

        var model = RouteAndPausesReportBuilder.BuildReport(
            stages, PeriodStart, PeriodEnd, ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(5, model.ColumnHeaders.Count);
        Assert.Equal(5, model.Rows[0].Cells.Count);
        Assert.DoesNotContain(model.ColumnHeaders, h => h.Contains("Пациент", StringComparison.Ordinal));
        Assert.DoesNotContain(model.ColumnHeaders, h => h.Contains("талон", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildReport_groups_rows_by_date()
    {
        var stages = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(10, new TimeOnly(10, 0), new TimeOnly(10, 10)),
            Obs(10, new TimeOnly(10, 20), new TimeOnly(10, 30)),
            Obs(20, new TimeOnly(10, 0), new TimeOnly(10, 10)),
            Obs(20, new TimeOnly(11, 0), new TimeOnly(11, 10))
        };

        var model = RouteAndPausesReportBuilder.BuildReport(
            stages, PeriodStart, PeriodEnd, ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(4, model.Rows.Count);
        Assert.Equal("2026-05-10", model.Rows[0].Cells[0]);
        Assert.Equal("", model.Rows[1].Cells[0]);
    }

    [Fact]
    public void BuildReport_sorts_by_pause_within_date()
    {
        var stages = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(10, new TimeOnly(10, 0), new TimeOnly(10, 10), new TimeOnly(9, 55)),
            Obs(10, new TimeOnly(10, 20), new TimeOnly(10, 30), new TimeOnly(10, 15)),
            Obs(20, new TimeOnly(10, 0), new TimeOnly(10, 10), new TimeOnly(9, 50)),
            Obs(20, new TimeOnly(11, 0), new TimeOnly(11, 10), new TimeOnly(10, 50))
        };

        var model = RouteAndPausesReportBuilder.BuildReport(
            stages, PeriodStart, PeriodEnd, ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(4, model.Rows.Count);
        Assert.True(
            double.Parse(model.Rows[0].Cells[4], CultureInfo.InvariantCulture)
            >= double.Parse(model.Rows[1].Cells[4], CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BuildReport_sets_preview_charts_from_full_data()
    {
        var stages = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(2, new TimeOnly(10, 0), new TimeOnly(10, 30)),
            Obs(2, new TimeOnly(11, 0), new TimeOnly(11, 30))
        };

        var model = RouteAndPausesReportBuilder.BuildReport(
            stages, PeriodStart, PeriodEnd, ReportGenerationPurpose.JsonPreview);

        Assert.NotNull(model.PreviewCharts);
        Assert.Single(model.PreviewCharts!);
        Assert.Equal("groupedBar", model.PreviewCharts[0].Kind);
        Assert.Equal("report-preview-chart-0", model.PreviewCharts[0].CanvasElementId);
    }

    [Fact]
    public void BuildReport_export_includes_period_totals_row()
    {
        var stages = new List<RouteAndPausesReportBuilder.RouteStageObservation>
        {
            Obs(2, new TimeOnly(10, 0), new TimeOnly(10, 30)),
            Obs(2, new TimeOnly(11, 0), new TimeOnly(11, 30))
        };

        var model = RouteAndPausesReportBuilder.BuildReport(
            stages, PeriodStart, PeriodEnd, ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(3, model.Rows.Count);
        Assert.Equal("Итого за период", model.Rows[1].Cells[0]);
        Assert.Equal("2", model.Rows[2].Cells[2]);
        Assert.Equal("60", model.Rows[2].Cells[3]);
        Assert.Equal("0", model.Rows[2].Cells[4]);
    }

    [Fact]
    public void GenerateRouteAndPausesOffline_does_not_throw_for_week_period()
    {
        var request = new ReportGenerateRequest
        {
            ReportId = ReportIds.RouteAndPauses,
            DateFrom = "2026-05-01 00:00:00",
            DateTo = "2026-05-07 23:59:59"
        };

        var model = MockReportGenerationService.GenerateRouteAndPausesOffline(request, ReportGenerationPurpose.JsonPreview);

        Assert.Equal(5, model.ColumnHeaders.Count);
        Assert.NotEmpty(model.Rows);
        Assert.NotNull(model.PreviewCharts);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    [InlineData("html")]
    [InlineData("pdf")]
    public void BuildExport_produces_non_empty_file_for_each_format(string format)
    {
        var request = new ReportExportRequest
        {
            ReportId = ReportIds.RouteAndPauses,
            DateFrom = "2026-05-01 00:00:00",
            DateTo = "2026-05-07 23:59:59",
            Format = format
        };

        var model = MockReportGenerationService.GenerateRouteAndPausesOffline(
            request,
            ReportGenerationPurpose.ExportOrFull);
        var (bytes, _, fileName) = ReportTabularExporter.Export(model, format, request);

        Assert.NotEmpty(bytes);
        Assert.EndsWith("." + format, fileName, StringComparison.OrdinalIgnoreCase);
    }

    private static RouteAndPausesReportBuilder.RouteStageObservation Obs(
        int idAppointment,
        TimeOnly? start,
        TimeOnly? end,
        TimeOnly? timeCall = null,
        TimeOnly? timeArrival = null,
        TimeOnly? timeComplete = null) =>
        new(
            idAppointment,
            Day,
            $"P{idAppointment}",
            timeArrival ?? DefaultArrival,
            timeComplete,
            timeCall,
            start,
            end);
}
