using WebApplication.Models;

namespace WebApplication.Services;

public sealed class ResilientReportGenerationService : IReportGenerationService
{
    private readonly IElectronicQueueAvailability _availability;
    private readonly ReportGenerationService _live;
    private readonly MockReportGenerationService _mock;

    public ResilientReportGenerationService(
        IElectronicQueueAvailability availability,
        ReportGenerationService live,
        MockReportGenerationService mock)
    {
        _availability = availability;
        _live = live;
        _mock = mock;
    }

    public IReadOnlyList<ReportSelectOption> GetCabinetOptions() =>
        _availability.TryGetCachedAvailability(out var ok) && ok
            ? _live.GetCabinetOptions()
            : _mock.GetCabinetOptions();

    public IReadOnlyList<ReportSelectOption> GetDoctorOptions() =>
        _availability.TryGetCachedAvailability(out var ok) && ok
            ? _live.GetDoctorOptions()
            : _mock.GetDoctorOptions();

    public IReadOnlyList<ReportSelectOption> GetCategoryOptions() =>
        _availability.TryGetCachedAvailability(out var ok) && ok
            ? _live.GetCategoryOptions()
            : _mock.GetCategoryOptions();

    public ReportGenerateResponse Generate(ReportGenerateRequest request) =>
        _availability.TryGetCachedAvailability(out var ok) && ok
            ? _live.Generate(request)
            : _mock.Generate(request);

    public (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request) =>
        _availability.TryGetCachedAvailability(out var ok) && ok
            ? _live.BuildExport(request)
            : _mock.BuildExport(request);

    public ReportResultViewModel GenerateQueueSummary(QueueSummaryReportParametersViewModel parameters) =>
        _availability.TryGetCachedAvailability(out var ok) && ok
            ? _live.GenerateQueueSummary(parameters)
            : _mock.GenerateQueueSummary(parameters);

    public ReportResultViewModel GenerateCabinetLoad(CabinetLoadReportParametersViewModel parameters) =>
        _availability.TryGetCachedAvailability(out var ok) && ok
            ? _live.GenerateCabinetLoad(parameters)
            : _mock.GenerateCabinetLoad(parameters);

    public byte[] BuildMockCsv(string reportId) =>
        _availability.TryGetCachedAvailability(out var ok) && ok
            ? _live.BuildMockCsv(reportId)
            : _mock.BuildMockCsv(reportId);
}
