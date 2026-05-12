using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;
using WebApplication.Services;

namespace WebApplication.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IQueueDashboardService _queueDashboard;
    private readonly UserManager<User> _userManager;
    private readonly IRolePermissionService _rolePermissionService;

    public DashboardController(
        IQueueDashboardService queueDashboard,
        UserManager<User> userManager,
        IRolePermissionService rolePermissionService)
    {
        _queueDashboard = queueDashboard;
        _userManager = userManager;
        _rolePermissionService = rolePermissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Мониторинг очереди";
        var model = await _queueDashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);

        var user = await _userManager.GetUserAsync(User).ConfigureAwait(false);
        var roleName = user is null
            ? "Registrator"
            : (await _userManager.GetRolesAsync(user).ConfigureAwait(false)).FirstOrDefault() ?? "Registrator";

        var permissionNames = await _rolePermissionService.GetPermissionNamesForRoleAsync(roleName, cancellationToken)
            .ConfigureAwait(false);
        model.Ui = _rolePermissionService.BuildDashboardVisibility(permissionNames);

        return View(model);
    }
}
