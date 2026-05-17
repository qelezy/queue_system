namespace WebApplication.Models;

public sealed class ReportsHubViewModel
{
    public IReadOnlyList<ReportCatalogItemViewModel> Catalog { get; init; } = [];
    public IReadOnlyList<ReportCategoryViewModel> CatalogByCategory { get; init; } = [];
    public string? SelectedReportId { get; init; }
    public string ToolbarDateFrom { get; set; } = "";
    public string ToolbarDateTo { get; set; } = "";
    public IReadOnlyList<ReportSelectOption> ToolbarCabinetOptions { get; set; } = [];
    public IReadOnlyList<ReportSelectOption> ToolbarDoctorOptions { get; set; } = [];
    public IReadOnlyList<ReportSelectOption> ToolbarCategoryOptions { get; set; } = [];

    public bool UsingElectronicQueueMockData { get; set; }
}
