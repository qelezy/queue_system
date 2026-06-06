using WebApplication.Models.Reports.Contracts;
using WebApplication.Services.Dashboard;
using WebApplication.Services.Demo;
using WebApplication.Services.Resilience;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ResilientReportExportTests
{
    private static (byte[] Bytes, string ContentType, string FileName) ExportStub(string fileName = "x.pdf") =>
        ([], "application/pdf", fileName);

    [Fact]
    public void TryLiveOrMockForExport_when_db_unavailable_and_not_development_throws()
    {
        var availability = new StubElectronicQueueAvailability(canConnectLive: false);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ResilientLiveMockExecutor.TryLiveOrMockForExport(
                availability,
                allowMockFallback: false,
                () => throw new InvalidOperationException("live should not run"),
                () => ExportStub()));

        Assert.Equal(ResilientLiveMockExecutor.ExportUnavailableMessage, ex.Message);
    }

    [Fact]
    public void TryLiveOrMockForExport_when_db_unavailable_and_development_uses_mock()
    {
        var availability = new StubElectronicQueueAvailability(canConnectLive: false);
        var usedMock = false;

        var result = ResilientLiveMockExecutor.TryLiveOrMockForExport(
            availability,
            allowMockFallback: true,
            () => throw new InvalidOperationException("live should not run"),
            () =>
            {
                usedMock = true;
                return ExportStub("mock.pdf");
            });

        Assert.True(usedMock);
        Assert.Equal("mock.pdf", result.FileName);
    }

    [Fact]
    public void TryLiveOrMockForExport_when_live_fails_and_not_development_rethrows()
    {
        var availability = new StubElectronicQueueAvailability(canConnectLive: true);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ResilientLiveMockExecutor.TryLiveOrMockForExport(
                availability,
                allowMockFallback: false,
                () => throw new TimeoutException("db timeout"),
                () => ExportStub()));

        Assert.Equal("Не удалось сформировать файл экспорта.", ex.Message);
        Assert.IsType<TimeoutException>(ex.InnerException);
        Assert.True(availability.TryGetCachedAvailability(out var ok));
        Assert.False(ok);
    }

    [Fact]
    public void MockServiceCategoriesComparisonOffline_has_eight_category_rows()
    {
        var request = new ReportGenerateRequest
        {
            ReportId = ReportIds.ServiceCategoriesComparison,
            DateFrom = "2025-06-01",
            DateTo = "2025-06-01"
        };

        var model = MockReportGenerationService.GenerateServiceCategoriesComparisonOffline(request);

        Assert.Equal(8, model.Rows.Count);
        Assert.Equal(11, model.ColumnHeaders.Count);
        foreach (var (_, label) in ElectronicQueueMockData.Categories)
            Assert.Contains(label, model.Rows.Select(r => r.Cells![0]));
    }

    private sealed class StubElectronicQueueAvailability : IElectronicQueueAvailability
    {
        private bool _canConnectLive;

        public StubElectronicQueueAvailability(bool canConnectLive) => _canConnectLive = canConnectLive;

        public Task<bool> CanQueryLiveDataAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_canConnectLive);

        public bool TryGetCachedAvailability(out bool canConnectLive)
        {
            canConnectLive = _canConnectLive;
            return true;
        }

        public void MarkUnavailable() => _canConnectLive = false;

        public void MarkAvailable() => _canConnectLive = true;
    }
}
