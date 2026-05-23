using WebApplication.Models.Reports.Configuration;

namespace WebApplication.Models.Configuration.Reports;

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

    /// <summary>Переопределение; если пусто — из <see cref="ReportCatalogDefaults"/> по Id.</summary>
    public string GeneratorKind { get; set; } = "";

    /// <summary>Переопределение; если пусто — из <see cref="ReportCatalogDefaults"/> по GeneratorKind.</summary>
    public string TableLayout { get; set; } = "";

    /// <summary>Переопределение; если пусто — из <see cref="ReportCatalogDefaults"/> по GeneratorKind.</summary>
    public string PdfOrientation { get; set; } = "";

    /// <summary>Переопределение; если пусто — из <see cref="ReportCatalogDefaults"/> по GeneratorKind.</summary>
    public string DetailRowKind { get; set; } = "";
}
