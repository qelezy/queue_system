using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WebApplication.Configuration;
using WebApplication.Services;
using WebApplication.Services.Common.Authorization;

namespace WebApplication.Configuration.DependencyInjection;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("JwtOptions"));

        var signingKey = JwtConfiguration.GetRequiredSigningKey(configuration);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["AppSettings:Issuer"],
                ValidateAudience = true,
                ValidAudience = configuration["AppSettings:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidateIssuerSigningKey = true
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.HttpContext.Items.TryGetValue("accessToken", out var runtimeTokenObj) &&
                        runtimeTokenObj is string runtimeToken &&
                        !string.IsNullOrWhiteSpace(runtimeToken))
                    {
                        context.Token = runtimeToken;
                        return Task.CompletedTask;
                    }

                    if (context.Request.Cookies.TryGetValue(AuthCookieHelper.AccessTokenCookieName, out var accessToken)
                        && !string.IsNullOrWhiteSpace(accessToken))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddScoped<IPasswordGeneratorService, PasswordGeneratorService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserPermissionContext, UserPermissionContext>();

        return services;
    }
}
