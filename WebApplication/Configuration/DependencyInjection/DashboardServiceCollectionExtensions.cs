using WebApplication.Models.Configuration;
using WebApplication.Services.Dashboard;

namespace WebApplication.Configuration.DependencyInjection;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationDashboard(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));
        services.AddSingleton<IDashboardPermissionsCatalog, DashboardPermissionsCatalog>();
        services.AddMemoryCache();
        services.AddSingleton<IDashboardHubConnectionTracker, DashboardHubConnectionTracker>();
        services.AddSingleton<IQueueDashboardClock, QueueDashboardClock>();
        services.AddScoped<IElectronicQueueAvailability, ElectronicQueueAvailabilityService>();
        services.AddScoped<QueueDashboardService>();
        services.AddScoped<IQueueDashboardService, QueueDashboardService>();
        services.AddHostedService<DashboardRefreshHostedService>();

        return services;
    }
}
