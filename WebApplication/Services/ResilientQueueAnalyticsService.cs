using WebApplication.Models;

namespace WebApplication.Services;

public sealed class ResilientQueueAnalyticsService : IQueueAnalyticsService
{
    private readonly IElectronicQueueAvailability _availability;
    private readonly QueueAnalyticsService _live;
    private readonly MockQueueAnalyticsService _mock;

    public ResilientQueueAnalyticsService(
        IElectronicQueueAvailability availability,
        QueueAnalyticsService live,
        MockQueueAnalyticsService mock)
    {
        _availability = availability;
        _live = live;
        _mock = mock;
    }

    public async Task<ManagerAnalyticsViewModel> GetManagerAnalyticsAsync(
        DateOnly from,
        DateOnly to,
        int? cabinetId,
        int? doctorId,
        int? categoryId,
        bool heatmapByDoctor,
        CancellationToken cancellationToken = default)
    {
        if (await _availability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false))
            return await _live.GetManagerAnalyticsAsync(from, to, cabinetId, doctorId, categoryId, heatmapByDoctor, cancellationToken).ConfigureAwait(false);
        return await _mock.GetManagerAnalyticsAsync(from, to, cabinetId, doctorId, categoryId, heatmapByDoctor, cancellationToken).ConfigureAwait(false);
    }
}
