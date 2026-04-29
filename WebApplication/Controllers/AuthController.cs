using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebApplication.Dto;
using WebApplication.Models;
using WebApplication.Services;
using System.Text.Encodings.Web;

namespace WebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private const string AccessTokenCookieName = "accessToken";
        private const string RefreshTokenCookieName = "refreshToken";
        private const string RememberMeCookieName = "rememberMe";
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly JwtOptions _jwtOptions;

        public AuthController(IAuthService authService, IEmailService emailService, IOptions<JwtOptions> jwtOptions)
        {
            _authService = authService;
            _emailService = emailService;
            _jwtOptions = jwtOptions.Value;
        }

        [HttpPost("login")]
        public async Task<ActionResult<TokenResponseDto>> Login(LoginRequestDto request)
        {
            var result = await _authService.LoginAsync(request);
            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });
            if (result.Data == null)
                return BadRequest(new { success = false, errors = new[] { "Не удалось получить токены" } });

            var isHttps = Request.IsHttps;
            var accessCookieOptions = BuildCookieOptions(request.RememberMe ? result.Data.Expires : null, isHttps);
            var refreshCookieOptions = BuildCookieOptions(request.RememberMe ? DateTime.UtcNow.AddDays(_jwtOptions.RefreshRememberDays) : null, isHttps);
            var rememberCookieOptions = BuildCookieOptions(request.RememberMe ? DateTime.UtcNow.AddDays(_jwtOptions.RefreshRememberDays) : null, isHttps);

            Response.Cookies.Append(AccessTokenCookieName, result.Data.AccessToken, accessCookieOptions);
            Response.Cookies.Append(RefreshTokenCookieName, result.Data.RefreshToken, refreshCookieOptions);
            Response.Cookies.Append(RememberMeCookieName, request.RememberMe ? "1" : "0", rememberCookieOptions);

            return Ok(new { success = true, data = result.Data });
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<TokenResponseDto>> RefreshToken(RefreshTokenRequestDto request)
        {
            var refreshToken = string.IsNullOrWhiteSpace(request.RefreshToken)
                ? Request.Cookies[RefreshTokenCookieName]
                : request.RefreshToken;

            var rememberMeEnabled = Request.Cookies.TryGetValue(RememberMeCookieName, out var rememberValue) && rememberValue == "1";
            var result = await _authService.RefreshTokenByTokenAsync(refreshToken ?? string.Empty, rememberMeEnabled);
            if (!result.Succeeded)
                return Unauthorized(new { success = false, errors = result.Errors });
            if (result.Data == null)
                return Unauthorized(new { success = false, errors = new[] { "Не удалось обновить токены" } });

            var isHttps = Request.IsHttps;
            Response.Cookies.Append(AccessTokenCookieName, result.Data.AccessToken, BuildCookieOptions(rememberMeEnabled ? result.Data.Expires : null, isHttps));
            Response.Cookies.Append(RefreshTokenCookieName, result.Data.RefreshToken, BuildCookieOptions(rememberMeEnabled ? DateTime.UtcNow.AddDays(_jwtOptions.RefreshRememberDays) : null, isHttps));
            Response.Cookies.Append(RememberMeCookieName, rememberMeEnabled ? "1" : "0", BuildCookieOptions(rememberMeEnabled ? DateTime.UtcNow.AddDays(_jwtOptions.RefreshRememberDays) : null, isHttps));

            return Ok(new { success = true, data = result.Data });
        }

        private static CookieOptions BuildCookieOptions(DateTime? expiresUtc, bool isHttps)
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

        [HttpGet("confirm-email")]
        public async Task<ActionResult> ConfirmEmail(Guid userId, string token)
        {
            var result = await _authService.ConfirmEmailAsync(userId, token);

            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordRequestDto request)
        {
            var result = await _authService.ForgotPasswordAsync(request);
            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            var resetLink = Url.Action(
                action: "ResetPassword",
                controller: "Account",
                values: new { userId = result.Data.UserId, token = result.Data.Token },
                protocol: Request.Scheme);

            await _emailService.SendEmailAsync(
                request.Email,
                "Сброс пароля",
                $@"
                <p>Чтобы сбросить пароль, перейдите по ссылке:</p>
                <a href='{HtmlEncoder.Default.Encode(resetLink)}'>Сбросить пароль</a>"
            );

            return Ok(new { success = true, message = "Ссылка для сброса пароля отправлена на email" });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<ActionResult> ResetPassword(PasswordResetTokenRequestDto request)
        {
            var result = await _authService.ResetPasswordAsync(request);
            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            return Ok(new { success = true, message = result.Message });
        }
    }
}
