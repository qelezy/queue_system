using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models.Configuration;

namespace WebApplication.Configuration.DependencyInjection;

public static class ElectronicQueueServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationElectronicQueue(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connections = configuration.GetSection(ConnectionStringsOptions.SectionName).Get<ConnectionStringsOptions>()
            ?? throw new InvalidOperationException("Секция ConnectionStrings не задана.");

        var electronicQueueConnection = string.IsNullOrWhiteSpace(connections.ElectronicQueue)
            ? throw new InvalidOperationException("Строка подключения ElectronicQueue не задана.")
            : connections.ElectronicQueue;

        services.AddDbContext<ElectronicQueueDbContext>(options =>
            options
                .UseSqlServer(electronicQueueConnection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        return services;
    }
}
