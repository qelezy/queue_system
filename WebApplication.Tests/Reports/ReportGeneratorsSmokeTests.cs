using System.Globalization;
using WebApplication.Models.Reports.Constants;
using WebApplication.Models.Reports.Contracts;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.Catalog;
using WebApplication.Services.Reports.LoadAndDowntime;
using Xunit;

namespace WebApplication.Tests.Reports;

[Trait(ElectronicQueueTestDb.RequiresDbTrait, "true")]
public sealed class ReportGeneratorsSmokeTests
{
    private static readonly DateTime PeriodFrom = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodTo = new(2026, 5, 19, 23, 59, 59, DateTimeKind.Utc);

    public static IEnumerable<object[]> AllReportCases =>
    [
        [ReportIds.LoadAndDowntime, new Dictionary<string, string?> { ["analysisMode"] = "doctor" }],
        [ReportIds.ServiceRouteOutcomes, new Dictionary<string, string?>()],
        [ReportIds.WaitingBeforeAppointment, new Dictionary<string, string?>()],
        [ReportIds.AppointmentDuration, new Dictionary<string, string?> { ["analysisMode"] = "doctor" }],
        [ReportIds.RouteAndPauses, new Dictionary<string, string?>()],
        [ReportIds.ServiceCategoriesComparison, new Dictionary<string, string?>()],
        [ReportIds.ServiceDelays, new Dictionary<string, string?> { ["analysisMode"] = "doctor" }]
    ];

    [Theory]
    [MemberData(nameof(AllReportCases))]
    public async Task Generate_live_report_returns_non_empty_table(
        string reportId,
        Dictionary<string, string?>? customParams)
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        await using var db = ElectronicQueueTestDb.CreateContext();
        var generator = ResolveGenerator(reportId);
        var request = BuildRequest(reportId, customParams);

        var response = generator.Generate(request, db, ReportGenerationPurpose.ExportOrFull);

        Assert.True(response.Success, response.Message);
        Assert.True(response.Implemented, reportId);
        Assert.NotNull(response.Result);
        Assert.NotEmpty(response.Result!.ColumnHeaders);
        Assert.NotEmpty(response.Result.Rows);
    }

    [Fact]
    public async Task ServiceRouteOutcomes_live_has_expected_column_count()
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        await using var db = ElectronicQueueTestDb.CreateContext();
        var generator = new ServiceRouteOutcomesReportGenerator();
        var response = generator.Generate(
            BuildRequest(ReportIds.ServiceRouteOutcomes, null),
            db,
            ReportGenerationPurpose.ExportOrFull);

        Assert.True(response.Implemented);
        Assert.NotNull(response.Result);
        Assert.Equal(5, response.Result!.ColumnHeaders.Count);
    }

    private static ReportGenerateRequest BuildRequest(
        string reportId,
        Dictionary<string, string?>? customParams) =>
        new()
        {
            ReportId = reportId,
            DateFrom = PeriodFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTo = PeriodTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            CustomParams = customParams ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

    private static IReportGenerator ResolveGenerator(string reportId) =>
        reportId switch
        {
            ReportIds.LoadAndDowntime => new LoadAndDowntimeReportGenerator(),
            ReportIds.ServiceRouteOutcomes => new ServiceRouteOutcomesReportGenerator(),
            ReportIds.WaitingBeforeAppointment => new WaitingBeforeAppointmentReportGenerator(),
            ReportIds.AppointmentDuration => new AppointmentDurationReportGenerator(),
            ReportIds.RouteAndPauses => new RouteAndPausesReportGenerator(),
            ReportIds.ServiceCategoriesComparison => new ServiceCategoriesComparisonReportGenerator(),
            ReportIds.ServiceDelays => new ServiceDelaysReportGenerator(),
            _ => throw new ArgumentOutOfRangeException(nameof(reportId), reportId, null)
        };
}
