using Microsoft.AspNetCore.Http;
using WebApplication.Dto;
using WebApplication.Models;

namespace WebApplication.Services
{
    public static class AuthCookieHelper
    {
        public const string AccessTokenCookieName = "accessToken";
        public const string RefreshTokenCookieName = "refreshToken";
        public const string RememberMeCookieName = "rememberMe";

        public static CookieOptions BuildAuthCookieOptions(DateTime? expiresUtc, bool isHttps)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiresUtc
            };
        }

        public static void AppendAuthCookies(HttpResponse response, TokenResponseDto tokens, JwtOptions jwtOptions, bool isHttps)
        {
            var extended = tokens.RefreshSessionExtended;
            DateTime? accessExpires = extended ? tokens.Expires : null;
            DateTime? slidingExpires = extended ? DateTime.UtcNow.AddDays(jwtOptions.RefreshRememberDays) : null;

            response.Cookies.Append(AccessTokenCookieName, tokens.AccessToken, BuildAuthCookieOptions(accessExpires, isHttps));
            response.Cookies.Append(RefreshTokenCookieName, tokens.RefreshToken, BuildAuthCookieOptions(slidingExpires, isHttps));
            response.Cookies.Append(RememberMeCookieName, extended ? "1" : "0", BuildAuthCookieOptions(slidingExpires, isHttps));
        }

        public static void DeleteAuthCookies(HttpResponse response)
        {
            var pathOpts = new CookieOptions { Path = "/" };
            response.Cookies.Delete(AccessTokenCookieName, pathOpts);
            response.Cookies.Delete(RefreshTokenCookieName, pathOpts);
            response.Cookies.Delete(RememberMeCookieName, pathOpts);
        }
    }
}
