using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using WebApplication.Services.Users;
using WebApplication.Models.Emails;
using WebApplication.Services.Emails;

namespace WebApplication.Controllers.Users;

[Route("api/users")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class UsersApiController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _emailTemplates;

    public UsersApiController(
        IUserService userService,
        IEmailService emailService,
        IEmailTemplateRenderer emailTemplates)
    {
        _userService = userService;
        _emailService = emailService;
        _emailTemplates = emailTemplates;
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
            values: new { userId = result.Data!.UserId, token = result.Data.Token },
            protocol: Request.Scheme);

        if (string.IsNullOrEmpty(confirmationLink))
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, errors = new[] { "Не удалось сформировать ссылку подтверждения" } });

        var body = await _emailTemplates.RenderRegistrationAsync(new RegistrationEmailViewModel
        {
            Password = result.Data.Password,
            ConfirmationLink = confirmationLink
        });

        await _emailService.SendEmailAsync(result.Data.Email, "Подтверждение почты", body);

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
