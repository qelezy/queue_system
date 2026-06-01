using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using WebApplication.Configuration;
using WebApplication.Models.Configuration;
using WebApplication.Services;
using WebApplication.Services.Auth;
using WebApplication.Services.Emails;
using WebApplication.Services.Common.Authorization;

namespace WebApplication.Configuration.DependencyInjection;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddWebApplicationAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AppSettingsOptions>(configuration.GetSection(AppSettingsOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection("JwtOptions"));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var appSettings = configuration.GetSection(AppSettingsOptions.SectionName).Get<AppSettingsOptions>()
            ?? throw new InvalidOperationException("Секция AppSettings не задана.");
        var signingKey = JwtConfiguration.GetRequiredSigningKey(appSettings);

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

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
                ValidIssuer = appSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = appSettings.Audience,
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
                },
                OnChallenge = context =>
                {
                    if (!ShouldRedirectUnauthenticatedBrowserToLogin(context.Request))
                        return Task.CompletedTask;

                    context.HandleResponse();
                    var returnUrl = (context.Request.PathBase + context.Request.Path + context.Request.QueryString).ToString();
                    var loginUrl = QueryHelpers.AddQueryString("/Account/Login", "returnUrl", returnUrl);
                    context.Response.Redirect(loginUrl);
                    return Task.CompletedTask;
                }
            };
        });

        services.AddScoped<IPasswordGeneratorService, PasswordGeneratorService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserPermissionContext, UserPermissionContext>();

        return services;
    }

    private static bool ShouldRedirectUnauthenticatedBrowserToLogin(HttpRequest request)
    {
        var path = request.Path.Value ?? "";
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        var accept = request.Headers.Accept.ToString();
        if (!string.IsNullOrEmpty(accept)
            && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)
            && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
