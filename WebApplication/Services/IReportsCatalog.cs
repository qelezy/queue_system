using WebApplication.Models;

namespace WebApplication.Services;

public interface IReportsCatalog
{
    IReadOnlyList<ReportCatalogItemViewModel> GetCatalog();
    IReadOnlyList<ReportCategoryViewModel> GetCatalogByCategory();
    bool TryGetItem(string? reportId, out ReportCatalogItemViewModel? item);
}
