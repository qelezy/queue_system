using WebApplication.Services.Admin;

namespace WebApplication.Configuration.DependencyInjection;

public static class AdminServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationAdmin(this IServiceCollection services)
    {
        services.AddScoped<IElectronicQueueAdminRepository, ElectronicQueueAdminRepository>();
        services.AddScoped<IServiceCategoryAdminService, ServiceCategoryAdminService>();
        return services;
    }
}
