namespace WebApplication.Models;

public sealed class ReportResultViewModel
{
    public string GeneratedForReportId { get; set; } = "";
    public string Title { get; set; } = "";
    public string DownloadFileName { get; set; } = "";
    public List<string> ColumnHeaders { get; set; } = new();
    public List<ReportResultRowViewModel> Rows { get; set; } = new();
}

public sealed class ReportResultRowViewModel
{
    public List<string> Cells { get; set; } = new();
}
