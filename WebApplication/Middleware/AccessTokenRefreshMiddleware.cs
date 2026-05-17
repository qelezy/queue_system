using Microsoft.Extensions.Options;
using WebApplication.Services;

namespace WebApplication.Middleware;

public sealed class AccessTokenRefreshMiddleware
{
    private readonly RequestDelegate _next;

    public AccessTokenRefreshMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        if (!context.Request.Cookies.TryGetValue(AuthCookieHelper.RefreshTokenCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Request.Cookies.TryGetValue(AuthCookieHelper.AccessTokenCookieName, out var accessToken);
        if (!AccessTokenRefreshGate.ShouldTryRefresh(accessToken))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var refreshResult = await authService.RefreshTokenByTokenAsync(refreshToken).ConfigureAwait(false);

        if (refreshResult.Succeeded && refreshResult.Data != null)
        {
            AuthCookieHelper.AppendAuthCookies(context.Response, refreshResult.Data, jwtOptions, context.Request.IsHttps);
            context.Items["accessToken"] = refreshResult.Data.AccessToken;
        }
        else
        {
            AuthCookieHelper.DeleteAuthCookies(context.Response);
        }

        await _next(context).ConfigureAwait(false);
    }
}

public static class AccessTokenRefreshMiddlewareExtensions
{
    public static IApplicationBuilder UseAccessTokenRefresh(this IApplicationBuilder app) =>
        app.UseMiddleware<AccessTokenRefreshMiddleware>();
}
