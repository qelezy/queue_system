using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Configuration.DependencyInjection;

public static class ElectronicQueueServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationElectronicQueue(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var electronicQueueConnection = configuration.GetConnectionString("ElectronicQueue")
            ?? throw new InvalidOperationException("Строка подключения ElectronicQueue не задана.");

        services.AddDbContext<ElectronicQueueDbContext>(options =>
            options
                .UseSqlServer(electronicQueueConnection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        return services;
    }
}
