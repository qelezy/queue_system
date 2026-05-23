using WebApplication.Services.Demo;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.Catalog;
using WebApplication.Services.Reports.LoadAndDowntime;
using WebApplication.Services.Resilience;

namespace WebApplication.Configuration.DependencyInjection;

public static class ReportsServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationReports(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<ReportsOptions>(configuration.GetSection("Reports"));
        services.AddSingleton<IReportsCatalog, ReportsCatalog>();
        services.AddSingleton<ReportCatalogMetadataEnricher>();
        services.AddScoped<IReportGenerator, LoadAndDowntimeReportGenerator>();
        services.AddScoped<IReportGenerator, ServiceRouteOutcomesReportGenerator>();
        services.AddScoped<IReportGenerator, WaitingBeforeAppointmentReportGenerator>();
        services.AddScoped<IReportGenerator, AppointmentDurationReportGenerator>();
        services.AddScoped<IReportGenerator, RouteAndPausesReportGenerator>();
        services.AddScoped<IReportGenerator, ServiceCategoriesComparisonReportGenerator>();
        services.AddScoped<IReportGenerator, ServiceDelaysReportGenerator>();
        services.AddScoped<ReportGeneratorRegistry>();
        services.AddScoped<ReportGenerationService>();

        if (environment.IsDevelopment())
        {
            services.AddScoped<MockReportGenerationService>();
            services.AddScoped<IReportGenerationService, ResilientReportGenerationService>();
        }
        else
        {
            services.AddScoped<IReportGenerationService, ReportGenerationService>();
        }

        return services;
    }
}
