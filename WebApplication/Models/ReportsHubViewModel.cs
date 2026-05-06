namespace WebApplication.Models;

public sealed class ReportsHubViewModel
{
    public IReadOnlyList<ReportCatalogItemViewModel> Catalog { get; init; } = [];
    public IReadOnlyList<ReportCategoryViewModel> CatalogByCategory { get; init; } = [];
    public string? SelectedReportId { get; init; }
    public ReportResultViewModel? LastResult { get; init; }
    public string ToolbarDateFrom { get; set; } = "";
    public string ToolbarDateTo { get; set; } = "";
    public string ToolbarWeekStart { get; set; } = "";
    public long? ToolbarCabinetId { get; set; }
    public long? ToolbarDoctorId { get; set; }
    public IReadOnlyList<ReportSelectOption> ToolbarCabinetOptions { get; set; } = [];
    public IReadOnlyList<ReportSelectOption> ToolbarDoctorOptions { get; set; } = [];
    public IReadOnlyList<ReportSelectOption> ToolbarCategoryOptions { get; set; } = [];

    public QueueSummaryReportParametersViewModel QueueSummaryParams { get; set; } = new();
    public CabinetLoadReportParametersViewModel CabinetLoadParams { get; set; } = new();

    public bool UsingElectronicQueueMockData { get; set; }
}
