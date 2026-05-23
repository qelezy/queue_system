using WebApplication.Services.Dashboard;
using WebApplication.Services.Demo;
using WebApplication.Services.Resilience;

namespace WebApplication.Configuration.DependencyInjection;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationDashboard(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));
        services.AddMemoryCache();
        services.AddScoped<IElectronicQueueAvailability, ElectronicQueueAvailabilityService>();
        services.AddScoped<QueueDashboardService>();

        if (environment.IsDevelopment())
        {
            services.AddScoped<MockQueueDashboardService>();
            services.AddScoped<IQueueDashboardService, ResilientQueueDashboardService>();
        }
        else
        {
            services.AddScoped<IQueueDashboardService, QueueDashboardService>();
        }

        services.AddHostedService<DashboardRefreshHostedService>();

        return services;
    }
}
