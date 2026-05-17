using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Configuration.DependencyInjection;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var userDatabaseConnection = configuration.GetConnectionString("UserDatabase")
            ?? throw new InvalidOperationException("Строка подключения UserDatabase не задана.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(userDatabaseConnection));

        services.AddIdentity<User, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 6;
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(1);
            options.SlidingExpiration = true;
        });

        return services;
    }
}
