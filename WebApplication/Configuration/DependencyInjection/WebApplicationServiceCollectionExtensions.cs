using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApplication.Configuration;
using WebApplication.Data;
using WebApplication.Models.Configuration;
using WebApplication.Services;
using WebApplication.Services.Dashboard;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.Catalog;
using WebApplication.Services.Reports.LoadAndDowntime;

namespace WebApplication.Configuration.DependencyInjection;

public static class WebApplicationServiceCollectionExtensions
{
    private static void ConfigureJsonOptions(JsonOptions options)
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    }

    public static IServiceCollection AddWebApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ConfigurationValidation.ValidateRequiredConfiguration(configuration);

        services.AddControllers().AddJsonOptions(ConfigureJsonOptions);
        services.AddOpenApi();
        services.AddWebApplicationIdentity(configuration);
        services.AddWebApplicationElectronicQueue(configuration);
        services.AddWebApplicationAuth(configuration);
        services.AddWebApplicationUsers();
        services.AddWebApplicationAdmin();
        services.AddWebApplicationDashboard(configuration);
        services.AddWebApplicationSignalR();
        services.AddWebApplicationReports(configuration, environment);

        services.AddControllersWithViews().AddJsonOptions(ConfigureJsonOptions);
        services.Configure<AntiforgeryOptions>(o =>
        {
            o.HeaderName = "RequestVerificationToken";
        });

        services.AddRouting(options => options.LowercaseUrls = true);

        return services;
    }

    public static void ValidateReportsConfiguration(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        using var validateScope = app.Services.CreateScope();
        var catalog = validateScope.ServiceProvider.GetRequiredService<IReportsCatalog>();
        var generators = validateScope.ServiceProvider.GetServices<IReportGenerator>().ToList();
        ReportsConfigurationValidator.Validate(catalog, generators);
    }

    public static void ValidateMonitoringConfiguration(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        using var validateScope = app.Services.CreateScope();
        var options = validateScope.ServiceProvider.GetRequiredService<IOptions<MonitoringOptions>>();
        var catalog = validateScope.ServiceProvider.GetRequiredService<IDashboardPermissionsCatalog>();
        MonitoringConfigurationValidator.Validate(options, catalog);
    }

    public static async Task ValidateWebApplicationDatabasesAsync(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("WebApplication.Startup");

        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!app.Configuration.GetValue("DOCKER_SKIP_EF_MIGRATE", false))
            await appDb.Database.MigrateAsync().ConfigureAwait(false);

        var queueDb = scope.ServiceProvider.GetRequiredService<ElectronicQueueDbContext>();
        try
        {
            var canConnect = await queueDb.Database.CanConnectAsync().ConfigureAwait(false);
            if (!canConnect)
            {
                logger.LogWarning(
                    "Не удалось подключиться к базе ElectronicQueue. Live-данные дашборда будут недоступны до восстановления связи.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Ошибка при проверке подключения к ElectronicQueue. Live-данные дашборда будут недоступны до восстановления связи.");
        }
    }

    public static async Task SeedWebApplicationDataAsync(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();

        string[] roles = { "Admin", "Manager", "Dispatcher" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
                await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(role)).ConfigureAwait(false);
        }

        var permissionService = scope.ServiceProvider.GetRequiredService<IRolePermissionService>();
        await permissionService.SyncPermissionsAndSeedDefaultsAsync().ConfigureAwait(false);

        await app.SeedDockerBootstrapAdminAsync().ConfigureAwait(false);
    }
}
