using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;
using WebApplication.Services;

namespace WebApplication.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IQueueDashboardService _queueDashboard;
    private readonly IQueueAnalyticsService _queueAnalytics;
    private readonly IElectronicQueueAvailability _queueAvailability;

    public DashboardController(
        IQueueDashboardService queueDashboard,
        IQueueAnalyticsService queueAnalytics,
        IElectronicQueueAvailability queueAvailability)
    {
        _queueDashboard = queueDashboard;
        _queueAnalytics = queueAnalytics;
        _queueAvailability = queueAvailability;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateOnly? from,
        DateOnly? to,
        int? cabinetId,
        int? doctorId,
        int? categoryId,
        bool heatmapByDoctor = false,
        CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Мониторинг очереди";

        var model = await _queueDashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);

        if (User.IsInRole("Manager") || User.IsInRole("Admin"))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var defaultFrom = today.AddDays(-6);
            var fromDo = from ?? defaultFrom;
            var toDo = to ?? today;
            model.Manager = await _queueAnalytics
                .GetManagerAnalyticsAsync(fromDo, toDo, cabinetId, doctorId, categoryId, heatmapByDoctor, cancellationToken)
                .ConfigureAwait(false);
        }

        var live = await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);
        model.UsingElectronicQueueMockData = !live;

        return View(model);
    }
}
