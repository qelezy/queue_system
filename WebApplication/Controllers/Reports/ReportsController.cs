using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Services.Dashboard;

namespace WebApplication.Controllers.Reports;

[Authorize]
public class ReportsController : Controller
{
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
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Отчёты";

        var permissionNames = await GetPermissionNamesAsync(cancellationToken).ConfigureAwait(false);
        var fullCatalog = _catalog.GetCatalog();
        var catalog = FilterCatalog(fullCatalog, permissionNames);
        var selectedId = NormalizeSelected(selected, catalog);

        var today = DateTime.UtcNow.Date;
        var range = ResolveToolbarRange(from, to, today);

        var hub = new ReportsHubViewModel
        {
            Catalog = catalog,
            CatalogByCategory = FilterCatalogByCategory(_catalog.GetCatalogByCategory(), permissionNames),
            SelectedReportId = selectedId,
            ToolbarDateFrom = range.From.ToString("yyyy-MM-dd"),
            ToolbarDateTo = range.To.ToString("yyyy-MM-dd"),
            ToolbarCabinetOptions = _generation.GetCabinetOptions().ToList(),
            ToolbarDoctorOptions = _generation.GetDoctorOptions().ToList(),
            ToolbarCategoryOptions = _generation.GetCategoryOptions().ToList()
        };

        return View(hub);
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

        try
        {
            var result = _generation.Generate(request, ReportGenerationPurpose.JsonPreview);
            ApplyReportPreviewRowLimit(result.Result);
            return Json(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ReportGenerateResponse
            {
                Success = false,
                Message = "Не удалось сформировать отчёт: " + ex.Message
            });
        }
    }

    private static void ApplyReportPreviewRowLimit(ReportResultViewModel? model)
    {
        
        if (model?.Rows is null || model.Rows.Count <= ReportPreviewLimits.MaxTableRows)
            return;
        model.PreviewRowsTotal = model.Rows.Count;
        model.PreviewRowLimit = ReportPreviewLimits.MaxTableRows;
        model.Rows = model.Rows.Take(ReportPreviewLimits.MaxTableRows).ToList();
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

        try
        {
            var built = _generation.BuildExport(request);
            return File(built.Bytes, built.ContentType, built.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new ReportGenerateResponse { Success = false, Message = ex.Message });
        }
    }

    private async Task<HashSet<string>> GetPermissionNamesAsync(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User).ConfigureAwait(false);
        var roleName = user is null
            ? "Dispatcher"
            : (await _userManager.GetRolesAsync(user).ConfigureAwait(false)).FirstOrDefault() ?? "Dispatcher";
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

    private static string? NormalizeSelected(string? selected, IReadOnlyList<ReportCatalogItemViewModel> catalog)
    {
        if (catalog.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(selected) &&
            catalog.FirstOrDefault(x => string.Equals(x.Id, selected.Trim(), StringComparison.OrdinalIgnoreCase)) is { } match)
            return match.Id;

        return null;
    }
}
