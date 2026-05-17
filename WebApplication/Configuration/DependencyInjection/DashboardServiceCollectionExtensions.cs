using WebApplication.Services;

namespace WebApplication.Configuration.DependencyInjection;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationDashboard(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));
        services.AddMemoryCache();
        services.AddScoped<IElectronicQueueAvailability, ElectronicQueueAvailabilityService>();
        services.AddScoped<QueueDashboardService>();
        services.AddScoped<MockQueueDashboardService>();
        services.AddScoped<IQueueDashboardService, ResilientQueueDashboardService>();
        return services;
    }
}
