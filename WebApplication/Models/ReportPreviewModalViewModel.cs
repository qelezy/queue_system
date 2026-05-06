namespace WebApplication.Models;

public sealed class ReportPreviewModalViewModel
{
    public string ModalId { get; init; } = "report-preview-modal";
    public string Title { get; init; } = "Предпросмотр отчёта";
    public ReportResultViewModel? Result { get; init; }
    public string? DownloadReportId { get; init; }
    public bool AutoOpen { get; init; }
}
