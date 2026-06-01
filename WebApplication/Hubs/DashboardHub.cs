using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebApplication.Services.Dashboard;

namespace WebApplication.Hubs;

[Authorize]
public sealed class DashboardHub : Hub
{
    private readonly IDashboardHubConnectionTracker _connectionTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(
        IDashboardHubConnectionTracker connectionTracker,
        IServiceScopeFactory scopeFactory,
        ILogger<DashboardHub> logger)
    {
        _connectionTracker = connectionTracker;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _connectionTracker.ConnectionOpened();
        try
        {
            await PushSnapshotToCallerAsync(Context.ConnectionAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initial dashboard snapshot failed for connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionTracker.ConnectionClosed();
        return base.OnDisconnectedAsync(exception);
    }

    private async Task PushSnapshotToCallerAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var availability = scope.ServiceProvider.GetRequiredService<IElectronicQueueAvailability>();
        if (!await availability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false))
            return;

        var dashboard = scope.ServiceProvider.GetRequiredService<IQueueDashboardService>();
        var model = await dashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
        var dto = DashboardSnapshotMapper.ToSnapshot(model);
        await Clients.Caller
            .SendAsync("DashboardUpdated", dto, cancellationToken)
            .ConfigureAwait(false);
    }
}
