using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using WebApplication.Dto;
using WebApplication.Services;
using System.Text.Encodings.Web;
using WebApplication.Models;

namespace WebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;

        public UserController(IUserService userService, IEmailService emailService)
        {
            _userService = userService;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<User>> Register(RegisterRequestDto request)
        {
            var result = await _userService.RegisterAsync(request);
            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            var confirmationLink = Url.Action(
                action: "ConfirmEmail",
                controller: "Account",
                values: new { userId = result.Data.UserId, token = result.Data.Token },
                protocol: Request.Scheme);

            await _emailService.SendEmailAsync(
                result.Data.Email,
                "Подтверждение почты",
                $@"
                <p>Вам создана учетная запись.</p>
                <p><b>Пароль:</b> {HtmlEncoder.Default.Encode(result.Data.Password)}</p>
                <p>Подтвердите ваш email по ссылке:</p>
                <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>Подтвердить почту</a>
                <p>После входа рекомендуется сменить пароль.</p>"
            );

            return Ok(new { success = true, data = result.Data });
        }

        [HttpPatch("{id}")]
        public async Task<ActionResult<User>> Update(string id, UserDto request)
        {
            var result = await _userService.UpdateAsync(id, request);
            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            return Ok(new { success = true, data = result.Data });
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(currentUserId) &&
                string.Equals(currentUserId, id, StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    success = false,
                    errors = new[] { "Нельзя удалить собственную учётную запись." }
                });
            }

            var result = await _userService.DeleteAsync(id);
            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            return Ok(new { success = true, message = result.Message });
        }
    }
}
