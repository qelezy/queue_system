
namespace WebApplication.Services.Reports;

public interface IReportsCatalog
{
    IReadOnlyList<ReportCatalogItemViewModel> GetCatalog();
    IReadOnlyList<ReportCategoryViewModel> GetCatalogByCategory();
    bool TryGetItem(string? reportId, out ReportCatalogItemViewModel? item);
    bool TryGetByGeneratorKind(ReportGeneratorKind kind, out ReportCatalogItemViewModel? item);
    IReadOnlyList<string> GetIdsWithTableLayout(string tableLayout);
    bool UsesDateRowspan(string? reportId);
    bool UsesPortraitPdf(string? reportId);
    string? GetDetailRowKind(string? reportId);
    IReadOnlyDictionary<string, string> GetTableLayoutByReportId();
    IReadOnlyDictionary<string, string> GetDetailRowKindByReportId();
}
