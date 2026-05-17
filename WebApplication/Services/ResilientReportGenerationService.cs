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
        TryLiveOrMock(_live.GetCabinetOptions, _mock.GetCabinetOptions);

    public IReadOnlyList<ReportSelectOption> GetDoctorOptions() =>
        TryLiveOrMock(_live.GetDoctorOptions, _mock.GetDoctorOptions);

    public IReadOnlyList<ReportSelectOption> GetCategoryOptions() =>
        TryLiveOrMock(_live.GetCategoryOptions, _mock.GetCategoryOptions);

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull) =>
        TryLiveOrMock(
            () =>
            {
                var liveResponse = _live.Generate(request, purpose);
                if (!liveResponse.Implemented && _mock.IsImplementedOffline(request.ReportId))
                    return _mock.Generate(request, purpose);
                return liveResponse;
            },
            () => _mock.Generate(request, purpose));

    public (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request) =>
        TryLiveOrMock(
            () => _live.BuildExport(request),
            () => _mock.BuildExport(request));

    public byte[] BuildMockCsv(string reportId, string? analysisMode = null) =>
        TryLiveOrMock(
            () => _live.BuildMockCsv(reportId, analysisMode),
            () => _mock.BuildMockCsv(reportId, analysisMode));

    private T TryLiveOrMock<T>(Func<T> live, Func<T> mock)
    {
        if (!_availability.TryGetCachedAvailability(out var ok) || !ok)
            return mock();

        Exception? liveFailure = null;
        try
        {
            return live();
        }
        catch (Exception ex)
        {
            liveFailure = ex;
            _availability.MarkUnavailable();
        }

        try
        {
            return mock();
        }
        catch (Exception mockEx) when (liveFailure is not null)
        {
            throw new InvalidOperationException(
                "Не удалось сформировать отчёт по демо-данным после сбоя подключения к БД.",
                mockEx);
        }
    }
}
