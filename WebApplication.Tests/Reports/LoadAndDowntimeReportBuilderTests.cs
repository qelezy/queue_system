using System.Globalization;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.LoadAndDowntime;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class LoadAndDowntimeReportBuilderTests
{
    private static readonly DateOnly Day = new(2026, 5, 10);
    private static readonly DateTime PeriodFrom = Day.ToDateTime(new TimeOnly(0, 0));
    private static readonly DateTime PeriodTo = Day.ToDateTime(new TimeOnly(23, 59, 59));

    private static readonly Dictionary<int, string> Doctors = new() { [1] = "Врач А" };
    private static readonly Dictionary<int, string> Cabinets = new() { [1] = "101", [2] = "102" };

    [Fact]
    public void BuildReport_single_window_busy_plus_idle_equals_window_minutes()
    {
        var logs = new List<LoadAndDowntimeReportBuilder.LogWorkLite>
        {
            new(1, 1, Day, new TimeOnly(8, 0), new TimeOnly(12, 0))
        };
        var items = new List<LoadAndDowntimeReportBuilder.ListRowLite>
        {
            Row(1, 1, 1, Day, "Обслуживание", new TimeOnly(9, 0), new TimeOnly(10, 0), new TimeOnly(8, 58))
        };

        var metrics = GetDetailMetrics(logs, items);

        Assert.Equal(240, metrics.Window, 1);
        Assert.Equal(62, metrics.Busy, 1);
        Assert.Equal(178, metrics.Idle, 1);
        AssertMetricsInvariant(metrics);
    }

    [Fact]
    public void BuildReport_two_windows_counts_idle_per_window_and_busy_only_in_second()
    {
        var logs = new List<LoadAndDowntimeReportBuilder.LogWorkLite>
        {
            new(1, 1, Day, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new(1, 1, Day, new TimeOnly(14, 0), new TimeOnly(18, 0))
        };
        var items = new List<LoadAndDowntimeReportBuilder.ListRowLite>
        {
            Row(1, 1, 1, Day, "Обслуживание", new TimeOnly(15, 0), new TimeOnly(16, 0), new TimeOnly(14, 58))
        };

        var metrics = GetDetailMetrics(logs, items);

        Assert.Equal(480, metrics.Window, 1);
        Assert.Equal(62, metrics.Busy, 1);
        Assert.Equal(418, metrics.Idle, 1);
        AssertMetricsInvariant(metrics);
    }

    [Fact]
    public void BuildReport_excludes_no_show_status_from_busy()
    {
        var logs = new List<LoadAndDowntimeReportBuilder.LogWorkLite>
        {
            new(1, 1, Day, new TimeOnly(8, 0), new TimeOnly(12, 0))
        };
        var items = new List<LoadAndDowntimeReportBuilder.ListRowLite>
        {
            Row(1, 1, 1, Day, "Не явился", new TimeOnly(9, 0), new TimeOnly(10, 0), new TimeOnly(8, 58))
        };

        var metrics = GetDetailMetrics(logs, items);

        Assert.Equal(0, metrics.Busy, 1);
        Assert.Equal(240, metrics.Idle, 1);
        AssertMetricsInvariant(metrics);
    }

    [Fact]
    public void BuildReport_list_item_without_matching_log_shift_has_zero_busy()
    {
        var logs = new List<LoadAndDowntimeReportBuilder.LogWorkLite>
        {
            new(1, 1, Day, new TimeOnly(8, 0), new TimeOnly(12, 0))
        };
        var items = new List<LoadAndDowntimeReportBuilder.ListRowLite>
        {
            Row(1, 1, 2, Day, "Обслуживание", new TimeOnly(9, 0), new TimeOnly(10, 0), new TimeOnly(8, 58))
        };

        var metrics = GetDetailMetrics(logs, items);

        Assert.Equal(0, metrics.Busy, 1);
        Assert.Equal(240, metrics.Idle, 1);
        AssertMetricsInvariant(metrics);
    }

    [Fact]
    public void BuildReport_clips_busy_to_report_period()
    {
        var periodFrom = Day.ToDateTime(new TimeOnly(10, 0));
        var periodTo = Day.ToDateTime(new TimeOnly(18, 0));
        var logs = new List<LoadAndDowntimeReportBuilder.LogWorkLite>
        {
            new(1, 1, Day, new TimeOnly(8, 0), new TimeOnly(12, 0))
        };
        var items = new List<LoadAndDowntimeReportBuilder.ListRowLite>
        {
            Row(1, 1, 1, Day, "Обслуживание", new TimeOnly(9, 0), new TimeOnly(10, 30), new TimeOnly(8, 58))
        };

        var model = LoadAndDowntimeReportBuilder.BuildReport(
            logs,
            items,
            Doctors,
            Cabinets,
            periodFrom,
            periodTo,
            byCabinet: false,
            ReportGenerationPurpose.ExportOrFull);

        var metrics = ParseFirstDetailMetrics(model);
        Assert.Equal(120, metrics.Window, 1);
        Assert.Equal(30, metrics.Busy, 1);
        Assert.Equal(90, metrics.Idle, 1);
        AssertMetricsInvariant(metrics);
    }

    [Fact]
    public void BuildReport_counts_call_to_end_without_start_servicing()
    {
        var logs = new List<LoadAndDowntimeReportBuilder.LogWorkLite>
        {
            new(1, 1, Day, new TimeOnly(8, 0), new TimeOnly(12, 0))
        };
        var items = new List<LoadAndDowntimeReportBuilder.ListRowLite>
        {
            new(
                1,
                1,
                1,
                Day,
                1,
                "Обслужен",
                new TimeOnly(9, 0),
                new TimeOnly(9, 0),
                new TimeOnly(10, 0),
                "Терапия")
        };

        var metrics = GetDetailMetrics(logs, items);

        Assert.Equal(60, metrics.Busy, 1);
        AssertMetricsInvariant(metrics);
    }

    [Fact]
    public void BuildReport_excludes_stages_without_time_call_from_busy()
    {
        var logs = new List<LoadAndDowntimeReportBuilder.LogWorkLite>
        {
            new(1, 1, Day, new TimeOnly(8, 0), new TimeOnly(12, 0))
        };
        var items = new List<LoadAndDowntimeReportBuilder.ListRowLite>
        {
            new(
                1,
                1,
                1,
                Day,
                1,
                "Обслужен",
                null,
                new TimeOnly(9, 0),
                new TimeOnly(10, 0),
                "Терапия")
        };

        var metrics = GetDetailMetrics(logs, items);

        Assert.Equal(0, metrics.Busy, 1);
        Assert.Equal(240, metrics.Idle, 1);
        AssertMetricsInvariant(metrics);
    }

    private static (double Window, double Busy, double Idle) GetDetailMetrics(
        IReadOnlyList<LoadAndDowntimeReportBuilder.LogWorkLite> logs,
        IReadOnlyList<LoadAndDowntimeReportBuilder.ListRowLite> items)
    {
        var model = LoadAndDowntimeReportBuilder.BuildReport(
            logs,
            items,
            Doctors,
            Cabinets,
            PeriodFrom,
            PeriodTo,
            byCabinet: false,
            ReportGenerationPurpose.ExportOrFull);

        return ParseFirstDetailMetrics(model);
    }

    private static (double Window, double Busy, double Idle) ParseFirstDetailMetrics(ReportResultViewModel model)
    {
        var detail = model.Rows.First(r =>
            string.IsNullOrWhiteSpace(r.RowClass)
            && r.Cells.Count >= 8
            && double.TryParse(r.Cells[5], NumberStyles.Any, CultureInfo.InvariantCulture, out _));

        return (
            double.Parse(detail.Cells[5], CultureInfo.InvariantCulture),
            double.Parse(detail.Cells[6], CultureInfo.InvariantCulture),
            double.Parse(detail.Cells[7], CultureInfo.InvariantCulture));
    }

    private static void AssertMetricsInvariant((double Window, double Busy, double Idle) metrics)
    {
        Assert.True(
            Math.Abs(metrics.Window - (metrics.Busy + metrics.Idle)) < 0.1,
            $"Expected window ≈ busy + idle, got {metrics.Window} vs {metrics.Busy} + {metrics.Idle}");
    }

    private static LoadAndDowntimeReportBuilder.ListRowLite Row(
        int apptId,
        int doctorId,
        int cabinetId,
        DateOnly date,
        string statusName,
        TimeOnly start,
        TimeOnly end,
        TimeOnly? timeCall = null) =>
        new(
            apptId,
            doctorId,
            cabinetId,
            date,
            1,
            statusName,
            timeCall ?? start.AddMinutes(-2),
            start,
            end,
            "Терапия");
}

[Trait(ElectronicQueueTestDb.RequiresDbTrait, "true")]
public sealed class LoadAndDowntimeReportLiveTests
{
    private static readonly DateTime PeriodFrom = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodTo = new(2026, 5, 19, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public async Task LoadAndDowntime_detail_rows_satisfy_window_equals_busy_plus_idle()
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        await using var db = ElectronicQueueTestDb.CreateContext();
        var generator = new LoadAndDowntimeReportGenerator();
        var response = generator.Generate(
            new ReportGenerateRequest
            {
                ReportId = ReportIds.LoadAndDowntime,
                DateFrom = PeriodFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTo = PeriodTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CustomParams = new Dictionary<string, string?> { ["analysisMode"] = "doctor" }
            },
            db,
            ReportGenerationPurpose.ExportOrFull);

        Assert.True(response.Success, response.Message);
        Assert.NotNull(response.Result);

        foreach (var row in response.Result!.Rows)
        {
            if (row.Cells.Count < 8)
                continue;
            if (!string.IsNullOrWhiteSpace(row.RowClass))
                continue;
            if (!double.TryParse(row.Cells[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var window))
                continue;
            if (!double.TryParse(row.Cells[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var busy))
                continue;
            if (!double.TryParse(row.Cells[7], NumberStyles.Any, CultureInfo.InvariantCulture, out var idle))
                continue;

            Assert.True(
                Math.Abs(window - (busy + idle)) < 0.2,
                $"Row window={window}, busy={busy}, idle={idle}, doctor={row.Cells.ElementAtOrDefault(2)}");
        }
    }
}
