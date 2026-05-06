using Microsoft.Extensions.Options;
using WebApplication.Models;

namespace WebApplication.Services;

public sealed class ReportsCatalog : IReportsCatalog
{
    private readonly IOptions<ReportsOptions> _options;

    public ReportsCatalog(IOptions<ReportsOptions> options)
    {
        _options = options;
    }

    public IReadOnlyList<ReportCatalogItemViewModel> GetCatalog()
    {
        return _options.Value.Catalog
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => new ReportCatalogItemViewModel
            {
                Id = x.Id.Trim(),
                Category = (x.Category ?? "").Trim(),
                Title = x.Title ?? "",
                Description = x.Description ?? ""
            })
            .ToList();
    }

    public IReadOnlyList<ReportCategoryViewModel> GetCatalogByCategory()
    {
        var catalog = GetCatalog();
        var categories = _options.Value.Categories ?? [];

        var categoryTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in categories)
        {
            var id = (c.Id ?? "").Trim();
            if (string.IsNullOrEmpty(id))
                continue;
            categoryTitles[id] = string.IsNullOrWhiteSpace(c.Title) ? id : c.Title!;
        }

        var assignedReportIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ReportCategoryViewModel>();
        var seenCategoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var catDef in categories)
        {
            var cid = (catDef.Id ?? "").Trim();
            if (string.IsNullOrEmpty(cid) || !seenCategoryIds.Add(cid))
                continue;

            var items = catalog
                .Where(r => string.Equals(r.Category, cid, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in items)
                assignedReportIds.Add(item.Id);

            if (items.Count == 0)
                continue;

            result.Add(new ReportCategoryViewModel
            {
                Id = cid,
                Title = categoryTitles.TryGetValue(cid, out var t) ? t : cid,
                Items = items
            });
        }

        var orphans = catalog.Where(r => !assignedReportIds.Contains(r.Id)).ToList();
        if (orphans.Count > 0)
        {
            result.Add(new ReportCategoryViewModel
            {
                Id = "other",
                Title = "Прочее",
                Items = orphans
            });
        }

        return result;
    }

    public bool TryGetItem(string? reportId, out ReportCatalogItemViewModel? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(reportId))
            return false;

        var found = GetCatalog().FirstOrDefault(x =>
            string.Equals(x.Id, reportId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (found == null)
            return false;

        item = found;
        return true;
    }
}
