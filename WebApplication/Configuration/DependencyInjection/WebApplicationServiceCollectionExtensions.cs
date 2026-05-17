using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using WebApplication.Services;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.Catalog;
using WebApplication.Services.Reports.LoadAndDowntime;

namespace WebApplication.Configuration.DependencyInjection;

public static class WebApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();
        services.AddOpenApi();
        services.AddWebApplicationIdentity(configuration);
        services.AddWebApplicationElectronicQueue(configuration);
        services.AddWebApplicationAuth(configuration);
        services.AddWebApplicationUsers();
        services.AddWebApplicationDashboard(configuration);
        services.AddWebApplicationReports(configuration);

        services.AddControllersWithViews();
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

    public static async Task SeedWebApplicationDataAsync(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WebApplication.Data.AppDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();

        string[] roles = { "Admin", "Manager", "Dispatcher" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
                await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(role)).ConfigureAwait(false);
        }

        var permissionService = scope.ServiceProvider.GetRequiredService<IRolePermissionService>();
        await permissionService.SyncPermissionsAndSeedDefaultsAsync().ConfigureAwait(false);
    }
}
