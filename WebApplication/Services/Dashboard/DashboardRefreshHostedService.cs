using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using WebApplication.Hubs;
using WebApplication.Models.Configuration;
using WebApplication.Models.ViewModels.Dashboard;

namespace WebApplication.Services.Dashboard;

public sealed class DashboardRefreshHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly IHostEnvironment _environment;
    private readonly MonitoringOptions _options;
    private readonly ILogger<DashboardRefreshHostedService> _logger;

    public DashboardRefreshHostedService(
        IServiceScopeFactory scopeFactory,
        IHubContext<DashboardHub> hubContext,
        IHostEnvironment environment,
        IOptions<MonitoringOptions> options,
        ILogger<DashboardRefreshHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _environment = environment;
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
        using var scope = _scopeFactory.CreateScope();
        var availability = scope.ServiceProvider.GetRequiredService<IElectronicQueueAvailability>();
        var dashboard = scope.ServiceProvider.GetRequiredService<IQueueDashboardService>();

        var canLive = await availability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);

        if (!_environment.IsDevelopment() && !canLive)
            return;

        DashboardViewModel model;
        var isDemoData = false;

        try
        {
            isDemoData = _environment.IsDevelopment() && !canLive;
            model = await dashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard live query failed; marking queue DB unavailable");
            availability.MarkUnavailable();

            if (!_environment.IsDevelopment())
                return;

            isDemoData = true;
            model = await dashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
        }

        var dto = DashboardSnapshotMapper.ToSnapshot(model, isDemoData);
        await _hubContext.Clients.All
            .SendAsync("DashboardUpdated", dto, cancellationToken)
            .ConfigureAwait(false);
    }
}
