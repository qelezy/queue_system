namespace WebApplication.Models.Reports.ViewModels;

public sealed class ReportCategoryViewModel
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public IReadOnlyList<ReportCatalogItemViewModel> Items { get; init; } = [];
}
