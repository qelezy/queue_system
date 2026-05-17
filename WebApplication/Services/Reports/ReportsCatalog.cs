using Microsoft.Extensions.Options;

namespace WebApplication.Services.Reports;

public sealed class ReportsCatalog : IReportsCatalog
{
    private readonly IOptions<ReportsOptions> _options;
    private IReadOnlyList<ReportCatalogItemViewModel>? _catalogCache;

    public ReportsCatalog(IOptions<ReportsOptions> options)
    {
        _options = options;
    }

    public IReadOnlyList<ReportCatalogItemViewModel> GetCatalog()
    {
        if (_catalogCache is not null)
            return _catalogCache;

        _catalogCache = _options.Value.Catalog
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(MapItem)
            .ToList();

        return _catalogCache;
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

            if (string.Equals(cid, "other", StringComparison.OrdinalIgnoreCase))
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
            var otherTitle = categoryTitles.TryGetValue("other", out var ot) ? ot : "Прочее";
            result.Add(new ReportCategoryViewModel
            {
                Id = "other",
                Title = otherTitle,
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

    public bool TryGetByGeneratorKind(ReportGeneratorKind kind, out ReportCatalogItemViewModel? item)
    {
        item = GetCatalog().FirstOrDefault(x => x.GeneratorKind == kind);
        return item is not null;
    }

    public IReadOnlyList<string> GetIdsWithTableLayout(string tableLayout) =>
        GetCatalog()
            .Where(x => string.Equals(x.TableLayout, tableLayout, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToList();

    public bool UsesDateRowspan(string? reportId) =>
        TryGetItem(reportId, out var item)
        && item is not null
        && string.Equals(item.TableLayout, ReportTableLayouts.DateRowspan, StringComparison.OrdinalIgnoreCase);

    public bool UsesPortraitPdf(string? reportId) =>
        TryGetItem(reportId, out var item)
        && item is not null
        && string.Equals(item.PdfOrientation, ReportPdfOrientations.Portrait, StringComparison.OrdinalIgnoreCase);

    public string? GetDetailRowKind(string? reportId) =>
        TryGetItem(reportId, out var item) && item is not null ? item.DetailRowKind : null;

    public IReadOnlyDictionary<string, string> GetTableLayoutByReportId() =>
        GetCatalog().ToDictionary(x => x.Id, x => x.TableLayout, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> GetDetailRowKindByReportId() =>
        GetCatalog().ToDictionary(x => x.Id, x => x.DetailRowKind, StringComparer.OrdinalIgnoreCase);

    private static ReportCatalogItemViewModel MapItem(ReportCatalogItemOptions x)
    {
        var id = x.Id.Trim();
        var kind = ReportGeneratorKindParser.ParseRequired(x.GeneratorKind, $"Reports:Catalog:{id}");

        var tableLayout = string.IsNullOrWhiteSpace(x.TableLayout)
            ? ReportTableLayouts.Standard
            : x.TableLayout.Trim();
        var pdfOrientation = string.IsNullOrWhiteSpace(x.PdfOrientation)
            ? ReportPdfOrientations.Landscape
            : x.PdfOrientation.Trim();
        var detailRowKind = string.IsNullOrWhiteSpace(x.DetailRowKind)
            ? ReportDetailRowKinds.Standard
            : x.DetailRowKind.Trim();

        return new ReportCatalogItemViewModel
        {
            Id = id,
            Category = (x.Category ?? "").Trim(),
            Title = x.Title ?? "",
            Description = x.Description ?? "",
            GeneratorKind = kind,
            TableLayout = tableLayout,
            PdfOrientation = pdfOrientation,
            DetailRowKind = detailRowKind
        };
    }
}
