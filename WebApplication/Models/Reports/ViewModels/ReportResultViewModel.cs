namespace WebApplication.Models.Reports.ViewModels;

public sealed class ReportResultViewModel
{
    public string GeneratedForReportId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string TableLayout { get; set; } = ReportTableLayouts.Standard;
    public string PdfOrientation { get; set; } = ReportPdfOrientations.Landscape;
    public string DetailRowKind { get; set; } = ReportDetailRowKinds.Standard;
    public string DownloadFileName { get; set; } = "";
    public List<string> ColumnHeaders { get; set; } = new();
    public List<ReportResultRowViewModel> Rows { get; set; } = new();

    public ReportPreviewPieChart? PreviewPieChart { get; set; }

    public List<ReportPreviewChartDescriptor>? PreviewCharts { get; set; }

    public int? PreviewRowsTotal { get; set; }

    public int? PreviewRowLimit { get; set; }
}

public sealed class ReportResultRowViewModel
{
    public List<string> Cells { get; set; } = new();

    public List<string>? CsvCells { get; set; }

    public List<int>? CellColSpans { get; set; }

    public string? RowClass { get; set; }

    public static ReportResultRowViewModel FromCells(
        IEnumerable<string> cells,
        string? rowClass = null,
        IReadOnlyList<int>? cellColSpans = null,
        IEnumerable<string>? csvCells = null)
    {
        var display = cells.ToList();
        return new()
        {
            Cells = display,
            CsvCells = csvCells?.ToList(),
            RowClass = rowClass,
            CellColSpans = cellColSpans?.ToList()
        };
    }
}
