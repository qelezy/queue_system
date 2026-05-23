namespace WebApplication.Configuration.DependencyInjection;

public static class SignalRServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationSignalR(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }
}
