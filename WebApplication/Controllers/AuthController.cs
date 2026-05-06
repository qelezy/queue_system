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

            AuthCookieHelper.AppendAuthCookies(Response, result.Data, _jwtOptions, Request.IsHttps);

            return Ok(new { success = true, data = result.Data });
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<TokenResponseDto>> RefreshToken(RefreshTokenRequestDto request)
        {
            var refreshToken = string.IsNullOrWhiteSpace(request.RefreshToken)
                ? Request.Cookies[AuthCookieHelper.RefreshTokenCookieName]
                : request.RefreshToken;

            var result = await _authService.RefreshTokenByTokenAsync(refreshToken ?? string.Empty);
            if (!result.Succeeded)
                return Unauthorized(new { success = false, errors = result.Errors });
            if (result.Data == null)
                return Unauthorized(new { success = false, errors = new[] { "Не удалось обновить токены" } });

            AuthCookieHelper.AppendAuthCookies(Response, result.Data, _jwtOptions, Request.IsHttps);

            return Ok(new { success = true, data = result.Data });
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
