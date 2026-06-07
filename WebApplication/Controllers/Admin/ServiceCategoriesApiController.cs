using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Services.Admin;

namespace WebApplication.Controllers.Admin;

[Route("api/service-categories")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public sealed class ServiceCategoriesApiController : ControllerBase
{
    private readonly IServiceCategoryAdminService _service;

    public ServiceCategoriesApiController(IServiceCategoryAdminService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var items = await _service.ListAsync(includeArchived, cancellationToken).ConfigureAwait(false);
        return Ok(new { success = true, data = items });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken = default)
    {
        var items = await _service.GetSettingOptionsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { success = true, data = items });
    }

    [HttpGet("specialties")]
    public async Task<IActionResult> Specialties(CancellationToken cancellationToken = default)
    {
        var items = await _service.GetSpecialtyOptionsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { success = true, data = items });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
            return NotFound(new { success = false, message = "Категория не найдена." });

        return Ok(new { success = true, data = item });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ServiceCategorySaveRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "Нет данных для сохранения." });

        var result = await _service.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            return BadRequest(ToErrorResponse(result));

        return Ok(new { success = true, data = result.Data });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] ServiceCategorySaveRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "Нет данных для сохранения." });

        var result = await _service.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (result.ErrorCode == "sharedSettingConfirmationRequired")
        {
            return Conflict(new
            {
                success = false,
                code = result.ErrorCode,
                message = result.Message,
                sharedCategoryCount = result.SharedCategoryCount,
                sharedCategoryNames = result.SharedCategoryNames
            });
        }

        if (!result.Succeeded)
            return BadRequest(ToErrorResponse(result));

        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, CancellationToken cancellationToken = default)
    {
        var result = await _service.ArchiveAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            return BadRequest(ToErrorResponse(result));

        return Ok(new { success = true, data = result.Data, message = "Категория архивирована." });
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken = default)
    {
        var result = await _service.RestoreAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            return BadRequest(ToErrorResponse(result));

        return Ok(new { success = true, data = result.Data, message = "Категория восстановлена." });
    }

    private static object ToErrorResponse(ServiceCategoryAdminResult result) =>
        new
        {
            success = false,
            message = result.Message,
            errors = result.Errors
        };
}
