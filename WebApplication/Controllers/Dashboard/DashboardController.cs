using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Services;
using WebApplication.Services.Common.Authorization;

namespace WebApplication.Controllers.Dashboard;

[Authorize]
public class DashboardController : Controller
{
    private readonly IQueueDashboardService _queueDashboard;
    private readonly IUserPermissionContext _permissionContext;
    private readonly IRolePermissionService _rolePermissionService;

    public DashboardController(
        IQueueDashboardService queueDashboard,
        IUserPermissionContext permissionContext,
        IRolePermissionService rolePermissionService)
    {
        _queueDashboard = queueDashboard;
        _permissionContext = permissionContext;
        _rolePermissionService = rolePermissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Мониторинг очереди";
        var model = await _queueDashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);

        var permissionNames = await _permissionContext.GetCurrentPermissionNamesAsync(cancellationToken)
            .ConfigureAwait(false);
        model.Ui = _rolePermissionService.BuildDashboardVisibility(permissionNames);

        return View(model);
    }
}
