using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Services;
using WebApplication.Services.Common.Authorization;
using WebApplication.Services.Dashboard;

namespace WebApplication.Controllers.Dashboard;

[Authorize]
public class DashboardController : Controller
{
    private readonly IQueueDashboardService _queueDashboard;
    private readonly IElectronicQueueAvailability _queueAvailability;
    private readonly IWebHostEnvironment _environment;
    private readonly IUserPermissionContext _permissionContext;
    private readonly IRolePermissionService _rolePermissionService;

    public DashboardController(
        IQueueDashboardService queueDashboard,
        IElectronicQueueAvailability queueAvailability,
        IWebHostEnvironment environment,
        IUserPermissionContext permissionContext,
        IRolePermissionService rolePermissionService)
    {
        _queueDashboard = queueDashboard;
        _queueAvailability = queueAvailability;
        _environment = environment;
        _permissionContext = permissionContext;
        _rolePermissionService = rolePermissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Мониторинг очереди";

        var permissionNames = await _permissionContext.GetCurrentPermissionNamesAsync(cancellationToken)
            .ConfigureAwait(false);
        var ui = _rolePermissionService.BuildDashboardVisibility(permissionNames);

        if (!_environment.IsDevelopment())
        {
            if (!await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false))
            {
                ViewData["QueueDatabaseUnavailable"] = true;
                return View(new DashboardViewModel { Ui = ui });
            }
        }

        try
        {
            var model = await _queueDashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
            model.Ui = ui;
            return View(model);
        }
        catch (Exception) when (!_environment.IsDevelopment())
        {
            ViewData["QueueDatabaseUnavailable"] = true;
            return View(new DashboardViewModel { Ui = ui });
        }
    }
}
