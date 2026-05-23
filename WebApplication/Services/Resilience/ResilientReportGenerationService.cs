using WebApplication.Services.Dashboard;
using WebApplication.Services.Demo;
using WebApplication.Services.Reports;

namespace WebApplication.Services.Resilience;

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
        ResilientLiveMockExecutor.TryLiveOrMock(_availability, _live.GetCabinetOptions, _mock.GetCabinetOptions);

    public IReadOnlyList<ReportSelectOption> GetDoctorOptions() =>
        ResilientLiveMockExecutor.TryLiveOrMock(_availability, _live.GetDoctorOptions, _mock.GetDoctorOptions);

    public IReadOnlyList<ReportSelectOption> GetCategoryOptions() =>
        ResilientLiveMockExecutor.TryLiveOrMock(_availability, _live.GetCategoryOptions, _mock.GetCategoryOptions);

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        if (!_availability.TryGetCachedAvailability(out var ok) || !ok)
            return TagDemo(_mock.Generate(request, purpose), isDemo: true);

        try
        {
            var liveResponse = _live.Generate(request, purpose);
            if (!liveResponse.Implemented && _mock.IsImplementedOffline(request.ReportId))
                return TagDemo(_mock.Generate(request, purpose), isDemo: true);

            return TagDemo(liveResponse, isDemo: false);
        }
        catch (Exception)
        {
            _availability.MarkUnavailable();
            return TagDemo(_mock.Generate(request, purpose), isDemo: true);
        }
    }

    public (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request) =>
        ResilientLiveMockExecutor.TryLiveOrMock(_availability,
            () => _live.BuildExport(request),
            () => _mock.BuildExport(request));

    public byte[] BuildDemoCsv(string reportId, string? analysisMode = null) =>
        ResilientLiveMockExecutor.TryLiveOrMock(_availability,
            () => _live.BuildDemoCsv(reportId, analysisMode),
            () => _mock.BuildDemoCsv(reportId, analysisMode));

    private static ReportGenerateResponse TagDemo(ReportGenerateResponse response, bool isDemo)
    {
        response.IsDemoData = isDemo;
        return response;
    }
}
