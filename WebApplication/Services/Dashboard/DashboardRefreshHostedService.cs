using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using WebApplication.Hubs;
using WebApplication.Models.Configuration;

namespace WebApplication.Services.Dashboard;

public sealed class DashboardRefreshHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly IDashboardHubConnectionTracker _connectionTracker;
    private readonly MonitoringOptions _options;
    private readonly ILogger<DashboardRefreshHostedService> _logger;

    public DashboardRefreshHostedService(
        IServiceScopeFactory scopeFactory,
        IHubContext<DashboardHub> hubContext,
        IDashboardHubConnectionTracker connectionTracker,
        IOptions<MonitoringOptions> options,
        ILogger<DashboardRefreshHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _connectionTracker = connectionTracker;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(3, _options.DashboardRefreshSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard refresh tick failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (_connectionTracker.ConnectionCount == 0)
            return;

        using var scope = _scopeFactory.CreateScope();
        var availability = scope.ServiceProvider.GetRequiredService<IElectronicQueueAvailability>();
        var dashboard = scope.ServiceProvider.GetRequiredService<IQueueDashboardService>();

        if (!await availability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false))
            return;

        try
        {
            var model = await dashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
            var dto = DashboardSnapshotMapper.ToSnapshot(model);
            await _hubContext.Clients.All
                .SendAsync("DashboardUpdated", dto, cancellationToken)
                .ConfigureAwait(false);

            availability.MarkAvailable();
            _logger.LogInformation(
                "Dashboard broadcast: waiting={Waiting}, inService={InService}, queueRows={QueueRows}",
                dto.WaitingCount,
                dto.InServiceCount,
                dto.ActiveQueue.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard live query failed; skipping broadcast for this tick");
        }
    }
}
