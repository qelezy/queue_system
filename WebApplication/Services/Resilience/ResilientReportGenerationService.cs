using Microsoft.Extensions.Hosting;
using WebApplication.Services.Dashboard;
using WebApplication.Services.Demo;
using WebApplication.Services.Reports;

namespace WebApplication.Services.Resilience;

public sealed class ResilientReportGenerationService : IReportGenerationService
{
    private readonly IElectronicQueueAvailability _availability;
    private readonly ReportGenerationService _live;
    private readonly MockReportGenerationService _mock;
    private readonly bool _allowMockExport;

    public ResilientReportGenerationService(
        IElectronicQueueAvailability availability,
        ReportGenerationService live,
        MockReportGenerationService mock,
        IHostEnvironment hostEnvironment)
    {
        _availability = availability;
        _live = live;
        _mock = mock;
        _allowMockExport = hostEnvironment.IsDevelopment();
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
            return _mock.Generate(request, purpose);

        try
        {
            var liveResponse = _live.Generate(request, purpose);
            if (!liveResponse.Implemented && _mock.IsImplementedOffline(request.ReportId))
                return _mock.Generate(request, purpose);

            return liveResponse;
        }
        catch (Exception)
        {
            _availability.MarkUnavailable();
            return _mock.Generate(request, purpose);
        }
    }

    public (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request) =>
        ResilientLiveMockExecutor.TryLiveOrMockForExport(
            _availability,
            _allowMockExport,
            () => _live.BuildExport(request),
            () => _mock.BuildExport(request));
}
