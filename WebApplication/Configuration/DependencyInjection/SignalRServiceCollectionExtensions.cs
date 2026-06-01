using System.Text.Json;

namespace WebApplication.Configuration.DependencyInjection;

public static class SignalRServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationSignalR(this IServiceCollection services)
    {
        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            });
        return services;
    }
}
