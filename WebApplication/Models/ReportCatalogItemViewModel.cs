namespace WebApplication.Models;

public sealed class ReportCatalogItemViewModel
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public ReportGeneratorKind GeneratorKind { get; init; }
    public string TableLayout { get; init; } = ReportTableLayouts.Standard;
    public string PdfOrientation { get; init; } = ReportPdfOrientations.Landscape;
    public string DetailRowKind { get; init; } = ReportDetailRowKinds.Standard;
}
