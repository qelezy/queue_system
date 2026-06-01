using System.Globalization;
using System.Text.Json;
using WebApplication.Models.Reports.Contracts;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.Catalog;
using WebApplication.Services.Reports.LoadAndDowntime;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ReportCustomParamsDeserializationTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Deserializes_customParams_with_ordinal_ignore_case_keys()
    {
        const string json = """{"reportId":"load-and-downtime","customParams":{"analysisMode":"cabinet"}}""";

        var request = JsonSerializer.Deserialize<ReportGenerateRequest>(json, ApiJsonOptions);

        Assert.NotNull(request);
        Assert.True(request!.CustomParams.Comparer.Equals(StringComparer.OrdinalIgnoreCase));
        Assert.True(request.CustomParams.TryGetValue("analysisMode", out var mode));
        Assert.Equal("cabinet", mode);
        Assert.True(request.CustomParams.TryGetValue("ANALYSISMODE", out var modeUpper));
        Assert.Equal("cabinet", modeUpper);
    }

    [Fact]
    public void Deserialized_analysisMode_reaches_load_and_downtime_generator_as_cabinet()
    {
        const string json = """{"reportId":"load-and-downtime","customParams":{"analysisMode":"cabinet"}}""";
        var request = JsonSerializer.Deserialize<ReportGenerateRequest>(json, ApiJsonOptions);
        Assert.NotNull(request);

        var byCabinet = request!.CustomParams is not null
                        && request.CustomParams.TryGetValue("analysisMode", out var am)
                        && string.Equals(am?.Trim(), "cabinet", StringComparison.OrdinalIgnoreCase);

        Assert.True(byCabinet);
    }

    [Fact]
    public void Deserialized_analysisMode_reaches_appointment_duration_generator_as_specialty()
    {
        const string json = """{"reportId":"appointment-duration","customParams":{"analysisMode":"specialty"}}""";
        var request = JsonSerializer.Deserialize<ReportGenerateRequest>(json, ApiJsonOptions);
        Assert.NotNull(request);

        var mode = AppointmentDurationReportBuilder.ParseAnalysisMode(request!.CustomParams);

        Assert.Equal(AppointmentDurationReportBuilder.ModeSpecialty, mode);
    }
}

[Trait(ElectronicQueueTestDb.RequiresDbTrait, "true")]
public sealed class ReportCustomParamsLiveTests
{
    private static readonly DateTime PeriodFrom = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodTo = new(2026, 5, 19, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public async Task LoadAndDowntime_doctor_vs_cabinet_changes_column_order()
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        await using var db = ElectronicQueueTestDb.CreateContext();
        var generator = new LoadAndDowntimeReportGenerator();

        var doctor = generator.Generate(
            BuildRequest(ReportIds.LoadAndDowntime, new Dictionary<string, string?> { ["analysisMode"] = "doctor" }),
            db,
            ReportGenerationPurpose.ExportOrFull);
        var cabinet = generator.Generate(
            BuildRequest(ReportIds.LoadAndDowntime, new Dictionary<string, string?> { ["analysisMode"] = "cabinet" }),
            db,
            ReportGenerationPurpose.ExportOrFull);

        Assert.True(doctor.Success, doctor.Message);
        Assert.True(cabinet.Success, cabinet.Message);
        Assert.NotNull(doctor.Result);
        Assert.NotNull(cabinet.Result);
        Assert.NotEmpty(doctor.Result!.Rows);
        Assert.NotEmpty(cabinet.Result!.Rows);
        Assert.Equal("Врач", doctor.Result.ColumnHeaders[2]);
        Assert.Equal("Кабинет", cabinet.Result.ColumnHeaders[2]);
    }

    [Theory]
    [InlineData("doctor", "Врач")]
    [InlineData("specialty", "Специальность")]
    [InlineData("cabinet", "Кабинет")]
    public async Task AppointmentDuration_analysisMode_changes_slice_column(
        string analysisMode,
        string expectedHeader)
    {
        if (!await ElectronicQueueTestDb.CanConnectAsync())
            return;

        await using var db = ElectronicQueueTestDb.CreateContext();
        var generator = new AppointmentDurationReportGenerator();
        var response = generator.Generate(
            BuildRequest(ReportIds.AppointmentDuration, new Dictionary<string, string?> { ["analysisMode"] = analysisMode }),
            db,
            ReportGenerationPurpose.ExportOrFull);

        Assert.True(response.Success, response.Message);
        Assert.NotNull(response.Result);
        Assert.NotEmpty(response.Result!.Rows);
        Assert.Equal(expectedHeader, response.Result.ColumnHeaders[1]);
    }

    private static ReportGenerateRequest BuildRequest(
        string reportId,
        Dictionary<string, string?> customParams) =>
        new()
        {
            ReportId = reportId,
            DateFrom = PeriodFrom.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTo = PeriodTo.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            CustomParams = customParams
        };
}
