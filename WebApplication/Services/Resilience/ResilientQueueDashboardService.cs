using WebApplication.Services.Dashboard;
using WebApplication.Services.Demo;

namespace WebApplication.Services.Resilience;

public sealed class ResilientQueueDashboardService : IQueueDashboardService
{
    private readonly IElectronicQueueAvailability _availability;
    private readonly QueueDashboardService _live;
    private readonly MockQueueDashboardService _mock;

    public ResilientQueueDashboardService(
        IElectronicQueueAvailability availability,
        QueueDashboardService live,
        MockQueueDashboardService mock)
    {
        _availability = availability;
        _live = live;
        _mock = mock;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (await _availability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false))
            return await _live.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
        return await _mock.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
    }
}
