using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models.Configuration;

namespace WebApplication.Configuration.DependencyInjection;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ConnectionStringsOptions>(configuration.GetSection(ConnectionStringsOptions.SectionName));

        var connections = configuration.GetSection(ConnectionStringsOptions.SectionName).Get<ConnectionStringsOptions>()
            ?? throw new InvalidOperationException("Секция ConnectionStrings не задана.");

        var userDatabaseConnection = string.IsNullOrWhiteSpace(connections.UserDatabase)
            ? throw new InvalidOperationException("Строка подключения UserDatabase не задана.")
            : connections.UserDatabase;

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
