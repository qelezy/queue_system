using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebApplication.Services;
using WebApplication.Models.Emails;
using WebApplication.Services.Emails;

namespace WebApplication.Controllers.Auth {
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateRenderer _emailTemplates;
        private readonly JwtOptions _jwtOptions;

        public AuthController(
            IAuthService authService,
            IEmailService emailService,
            IEmailTemplateRenderer emailTemplates,
            IOptions<JwtOptions> jwtOptions)
        {
            _authService = authService;
            _emailService = emailService;
            _emailTemplates = emailTemplates;
            _jwtOptions = jwtOptions.Value;
        }

        [HttpPost("login")]
        [AllowAnonymous]
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
        [AllowAnonymous]
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
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(Guid userId, string token)
        {
            var result = await _authService.ConfirmEmailAsync(userId, token);

            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            return Ok(new { success = true, message = result.Message });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordRequestDto request)
        {
            var result = await _authService.ForgotPasswordAsync(request);
            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            if (result.Data is null)
                return BadRequest(new { success = false, errors = new[] { "Не удалось сформировать ссылку для сброса пароля" } });

            var resetLink = Url.Action(
                action: "ResetPassword",
                controller: "Account",
                values: new { userId = result.Data.UserId, token = result.Data.Token },
                protocol: Request.Scheme);

            if (string.IsNullOrEmpty(resetLink))
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, errors = new[] { "Не удалось сформировать ссылку для сброса пароля" } });

            var body = await _emailTemplates.RenderPasswordResetAsync(new PasswordResetEmailViewModel
            {
                ResetLink = resetLink
            });

            await _emailService.SendEmailAsync(request.Email, "Сброс пароля", body);

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
