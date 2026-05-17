using WebApplication.Services.Users;

namespace WebApplication.Configuration.DependencyInjection;

public static class UsersServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationUsers(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        return services;
    }
}
