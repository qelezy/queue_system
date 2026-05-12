using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;
using WebApplication.Services;

namespace WebApplication.Controllers;

[Authorize]
public class ReportsController : Controller
{
    public const string TempDataReportResultKey = "reportResultJson";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IReportsCatalog _catalog;
    private readonly IReportGenerationService _generation;
    private readonly IElectronicQueueAvailability _queueAvailability;
    private readonly UserManager<User> _userManager;
    private readonly IRolePermissionService _rolePermissionService;

    public ReportsController(
        IReportsCatalog catalog,
        IReportGenerationService generation,
        IElectronicQueueAvailability queueAvailability,
        UserManager<User> userManager,
        IRolePermissionService rolePermissionService)
    {
        _catalog = catalog;
        _generation = generation;
        _queueAvailability = queueAvailability;
        _userManager = userManager;
        _rolePermissionService = rolePermissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? selected,
        string? from,
        string? to,
        long? cabinetId,
        long? doctorId,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Отчёты";

        var live = await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);

        var permissionNames = await GetPermissionNamesAsync(cancellationToken).ConfigureAwait(false);
        var fullCatalog = _catalog.GetCatalog();
        var catalog = FilterCatalog(fullCatalog, permissionNames);
        var selectedId = NormalizeSelected(selected, catalog);

        var lastResult = TryConsumeResultFromTempData();

        var today = DateTime.UtcNow.Date;
        var range = ResolveToolbarRange(from, to, today);
        var queueParams = CreateDefaultQueueSummaryParams(range.To);
        queueParams.DateFrom = range.From.ToString("yyyy-MM-dd");
        queueParams.DateTo = range.To.ToString("yyyy-MM-dd");
        queueParams.CabinetId = cabinetId;
        queueParams.DoctorId = doctorId;
        FillSelectOptions(queueParams);

        var hub = new ReportsHubViewModel
        {
            Catalog = catalog,
            CatalogByCategory = FilterCatalogByCategory(_catalog.GetCatalogByCategory(), permissionNames),
            SelectedReportId = selectedId,
            LastResult = lastResult,
            ToolbarDateFrom = queueParams.DateFrom,
            ToolbarDateTo = queueParams.DateTo,
            ToolbarWeekStart = CreateDefaultCabinetLoadParams(range.From).WeekStart,
            ToolbarCabinetId = queueParams.CabinetId,
            ToolbarDoctorId = queueParams.DoctorId,
            ToolbarCabinetOptions = queueParams.CabinetOptions,
            ToolbarDoctorOptions = queueParams.DoctorOptions,
            ToolbarCategoryOptions = _generation.GetCategoryOptions(),
            QueueSummaryParams = queueParams,
            CabinetLoadParams = CreateDefaultCabinetLoadParams(range.From),
            UsingElectronicQueueMockData = !live
        };

        return View(hub);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunQueueSummary(
        QueueSummaryReportParametersViewModel model,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessReportAsync(ReportIds.QueueSummary, cancellationToken).ConfigureAwait(false))
            return Forbid();

        await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);
        FillSelectOptions(model);
        if (!ModelState.IsValid)
            return await InvalidQueueSummary(model, cancellationToken).ConfigureAwait(false);

        var result = _generation.GenerateQueueSummary(model);
        TempData[TempDataReportResultKey] = JsonSerializer.Serialize(result, JsonSerializerOptions);
        return RedirectToAction(nameof(Index), new
        {
            selected = ReportIds.QueueSummary,
            from = model.DateFrom,
            to = model.DateTo,
            cabinetId = model.CabinetId,
            doctorId = model.DoctorId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunCabinetLoad(
        CabinetLoadReportParametersViewModel model,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessReportAsync(ReportIds.CabinetLoad, cancellationToken).ConfigureAwait(false))
            return Forbid();

        await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);
        if (!ModelState.IsValid)
            return await InvalidCabinetLoad(model, cancellationToken).ConfigureAwait(false);

        var result = _generation.GenerateCabinetLoad(model);
        TempData[TempDataReportResultKey] = JsonSerializer.Serialize(result, JsonSerializerOptions);
        if (!DateTime.TryParse(model.WeekStart, out var weekStart))
            weekStart = DateTime.UtcNow.Date;
        return RedirectToAction(nameof(Index), new
        {
            selected = ReportIds.CabinetLoad,
            from = weekStart.ToString("yyyy-MM-dd"),
            to = weekStart.AddDays(6).ToString("yyyy-MM-dd")
        });
    }

    [HttpGet]
    public async Task<IActionResult> Download(string reportId, CancellationToken cancellationToken)
    {
        if (!_catalog.TryGetItem(reportId, out _))
            return NotFound();

        if (!await CanAccessReportAsync(reportId, cancellationToken).ConfigureAwait(false))
            return Forbid();

        await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);
        var bytes = _generation.BuildMockCsv(reportId);
        var fileName = $"{reportId.Trim().ToLowerInvariant()}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate([FromBody] ReportGenerateRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ReportId))
            return BadRequest(new ReportGenerateResponse { Success = false, Message = "Не указан идентификатор отчета." });
        NormalizePeriod(request);

        await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);
        if (!_catalog.TryGetItem(request.ReportId, out _))
            return NotFound(new ReportGenerateResponse { Success = false, Message = "Отчет не найден." });

        if (!await CanAccessReportAsync(request.ReportId, cancellationToken).ConfigureAwait(false))
            return Json(new ReportGenerateResponse { Success = false, Message = "Нет доступа к этому отчёту." });

        var result = _generation.Generate(request);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export([FromBody] ReportExportRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ReportId))
            return BadRequest();
        NormalizePeriod(request);

        await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);
        if (!_catalog.TryGetItem(request.ReportId, out _))
            return NotFound();

        if (!await CanAccessReportAsync(request.ReportId, cancellationToken).ConfigureAwait(false))
            return Forbid();

        var built = _generation.BuildExport(request);
        return File(built.Bytes, built.ContentType, built.FileName);
    }

    private async Task<HashSet<string>> GetPermissionNamesAsync(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User).ConfigureAwait(false);
        var roleName = user is null
            ? "Registrator"
            : (await _userManager.GetRolesAsync(user).ConfigureAwait(false)).FirstOrDefault() ?? "Registrator";
        return await _rolePermissionService.GetPermissionNamesForRoleAsync(roleName, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> CanAccessReportAsync(string reportId, CancellationToken cancellationToken)
    {
        var perms = await GetPermissionNamesAsync(cancellationToken).ConfigureAwait(false);
        return perms.Contains(reportId);
    }

    private static List<ReportCatalogItemViewModel> FilterCatalog(
        IReadOnlyList<ReportCatalogItemViewModel> full,
        HashSet<string> permissionNames) =>
        full.Where(x => permissionNames.Contains(x.Id)).ToList();

    private static List<ReportCategoryViewModel> FilterCatalogByCategory(
        IReadOnlyList<ReportCategoryViewModel> full,
        HashSet<string> permissionNames)
    {
        var result = new List<ReportCategoryViewModel>();
        foreach (var c in full)
        {
            var items = c.Items.Where(x => permissionNames.Contains(x.Id)).ToList();
            if (items.Count > 0)
            {
                result.Add(new ReportCategoryViewModel
                {
                    Id = c.Id,
                    Title = c.Title,
                    Items = items
                });
            }
        }

        return result;
    }

    private async Task<IActionResult> InvalidQueueSummary(
        QueueSummaryReportParametersViewModel model,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Отчёты";
        var live = await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);
        var permissionNames = await GetPermissionNamesAsync(cancellationToken).ConfigureAwait(false);
        var fullCatalog = _catalog.GetCatalog();
        var catalog = FilterCatalog(fullCatalog, permissionNames);
        FillSelectOptions(model);
        var hub = new ReportsHubViewModel
        {
            Catalog = catalog,
            CatalogByCategory = FilterCatalogByCategory(_catalog.GetCatalogByCategory(), permissionNames),
            SelectedReportId = ReportIds.QueueSummary,
            LastResult = null,
            ToolbarDateFrom = model.DateFrom,
            ToolbarDateTo = model.DateTo,
            ToolbarWeekStart = CreateDefaultCabinetLoadParams(DateTime.UtcNow.Date).WeekStart,
            ToolbarCabinetId = model.CabinetId,
            ToolbarDoctorId = model.DoctorId,
            ToolbarCabinetOptions = model.CabinetOptions,
            ToolbarDoctorOptions = model.DoctorOptions,
            ToolbarCategoryOptions = _generation.GetCategoryOptions(),
            QueueSummaryParams = model,
            CabinetLoadParams = CreateDefaultCabinetLoadParams(DateTime.UtcNow.Date),
            UsingElectronicQueueMockData = !live
        };
        return View("Index", hub);
    }

    private async Task<IActionResult> InvalidCabinetLoad(
        CabinetLoadReportParametersViewModel model,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Отчёты";
        var live = await _queueAvailability.CanQueryLiveDataAsync(cancellationToken).ConfigureAwait(false);
        var permissionNames = await GetPermissionNamesAsync(cancellationToken).ConfigureAwait(false);
        var fullCatalog = _catalog.GetCatalog();
        var catalog = FilterCatalog(fullCatalog, permissionNames);
        var hub = new ReportsHubViewModel
        {
            Catalog = catalog,
            CatalogByCategory = FilterCatalogByCategory(_catalog.GetCatalogByCategory(), permissionNames),
            SelectedReportId = ReportIds.CabinetLoad,
            LastResult = null,
            QueueSummaryParams = CreateDefaultQueueSummaryParams(DateTime.UtcNow.Date),
            CabinetLoadParams = model,
            UsingElectronicQueueMockData = !live
        };
        hub.ToolbarDateFrom = hub.QueueSummaryParams.DateFrom;
        hub.ToolbarDateTo = hub.QueueSummaryParams.DateTo;
        hub.ToolbarWeekStart = model.WeekStart;
        hub.ToolbarCabinetId = hub.QueueSummaryParams.CabinetId;
        hub.ToolbarDoctorId = hub.QueueSummaryParams.DoctorId;
        hub.ToolbarCabinetOptions = hub.QueueSummaryParams.CabinetOptions;
        hub.ToolbarDoctorOptions = hub.QueueSummaryParams.DoctorOptions;
        hub.ToolbarCategoryOptions = _generation.GetCategoryOptions();
        return View("Index", hub);
    }

    private void FillSelectOptions(QueueSummaryReportParametersViewModel model)
    {
        model.CabinetOptions = _generation.GetCabinetOptions().ToList();
        model.DoctorOptions = _generation.GetDoctorOptions().ToList();
    }

    private QueueSummaryReportParametersViewModel CreateDefaultQueueSummaryParams(DateTime today)
    {
        var from = today.AddDays(-6);
        var m = new QueueSummaryReportParametersViewModel
        {
            DateFrom = from.ToString("yyyy-MM-dd"),
            DateTo = today.ToString("yyyy-MM-dd")
        };
        FillSelectOptions(m);
        return m;
    }

    private static (DateTime From, DateTime To) ResolveToolbarRange(string? from, string? to, DateTime today)
    {
        var toDate = TryParseDateOrDefault(to, today);
        var fromDate = TryParseDateOrDefault(from, toDate.AddDays(-6));
        if (fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        return (fromDate.Date, toDate.Date);
    }

    private static DateTime TryParseDateOrDefault(string? raw, DateTime fallback)
    {
        return DateTime.TryParse(raw, out var parsed)
            ? parsed.Date
            : fallback.Date;
    }

    private static void NormalizePeriod(ReportGenerateRequest request)
    {
        var now = DateTime.UtcNow;
        var from = DateTime.TryParse(request.DateFrom, out var parsedFrom) ? parsedFrom : now.Date.AddDays(-6);
        var to = DateTime.TryParse(request.DateTo, out var parsedTo) ? parsedTo : now;
        if (from > to)
            (from, to) = (to, from);

        request.DateFrom = from.ToString("yyyy-MM-dd HH:mm:ss");
        request.DateTo = to.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static CabinetLoadReportParametersViewModel CreateDefaultCabinetLoadParams(DateTime today)
    {
        var d = (int)today.DayOfWeek;
        var diff = d == (int)DayOfWeek.Sunday ? 6 : d - (int)DayOfWeek.Monday;
        var weekStart = today.AddDays(-diff);

        return new CabinetLoadReportParametersViewModel
        {
            WeekStart = weekStart.ToString("yyyy-MM-dd")
        };
    }

    private static string? NormalizeSelected(string? selected, IReadOnlyList<ReportCatalogItemViewModel> catalog)
    {
        if (catalog.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(selected) &&
            catalog.FirstOrDefault(x => string.Equals(x.Id, selected.Trim(), StringComparison.OrdinalIgnoreCase)) is { } match)
            return match.Id;

        return null;
    }

    private ReportResultViewModel? TryConsumeResultFromTempData()
    {
        if (!TempData.TryGetValue(TempDataReportResultKey, out var value))
            return null;

        var raw = value as string;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ReportResultViewModel>(raw, JsonSerializerOptions);
        }
        catch
        {
            return null;
        }
    }
}
