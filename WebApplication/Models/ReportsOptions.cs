namespace WebApplication.Models;

public sealed class ReportsOptions
{
    public List<ReportCategoryOptions> Categories { get; set; } = new();
    public List<ReportCatalogItemOptions> Catalog { get; set; } = new();
}

public sealed class ReportCategoryOptions
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
}

public sealed class ReportCatalogItemOptions
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}
