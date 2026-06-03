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
    private readonly IUserPermissionContext _permissionContext;
    private readonly IRolePermissionService _rolePermissionService;

    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IQueueDashboardService queueDashboard,
        IElectronicQueueAvailability queueAvailability,
        IUserPermissionContext permissionContext,
        IRolePermissionService rolePermissionService,
        ILogger<DashboardController> logger)
    {
        _queueDashboard = queueDashboard;
        _queueAvailability = queueAvailability;
        _permissionContext = permissionContext;
        _rolePermissionService = rolePermissionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Мониторинг";

        var permissionNames = await _permissionContext.GetCurrentPermissionNamesAsync(cancellationToken)
            .ConfigureAwait(false);
        var ui = _rolePermissionService.BuildDashboardVisibility(permissionNames);

        if (!await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false))
        {
            ViewData["QueueDatabaseUnavailable"] = true;
            return View(new DashboardViewModel { Ui = ui });
        }

        try
        {
            var model = await _queueDashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
            model.Ui = ui;
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard snapshot failed");
            return View(new DashboardViewModel { Ui = ui });
        }
    }

    [HttpGet("/dashboard/appointments/{id:int}/route-stages")]
    public async Task<IActionResult> GetRouteStages(int id, CancellationToken cancellationToken = default)
    {
        if (!await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false))
            return StatusCode(503);

        try
        {
            var result = await _queueDashboard.GetRouteStagesAsync(id, cancellationToken)
                .ConfigureAwait(false);
            if (result == null)
                return NotFound();
            return Json(result);
        }
        catch (Exception)
        {
            return StatusCode(503);
        }
    }
}
