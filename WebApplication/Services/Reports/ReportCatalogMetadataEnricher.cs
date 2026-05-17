
namespace WebApplication.Services.Reports;

public sealed class ReportCatalogMetadataEnricher
{
    private readonly IReportsCatalog _catalog;

    public ReportCatalogMetadataEnricher(IReportsCatalog catalog)
    {
        _catalog = catalog;
    }

    public void ApplyToResult(ReportResultViewModel result, string reportId)
    {
        var id = reportId.Trim();
        result.GeneratedForReportId = id;
        result.DownloadFileName = $"{id}.csv";

        if (_catalog.TryGetItem(id, out var item) && item is not null)
        {
            result.Title = item.Title;
            result.Description = item.Description;
            result.TableLayout = item.TableLayout;
            result.PdfOrientation = item.PdfOrientation;
            result.DetailRowKind = item.DetailRowKind;
            ApplyChartAriaLabels(result, item.Title);
        }
    }

    private static void ApplyChartAriaLabels(ReportResultViewModel result, string reportTitle)
    {
        if (string.IsNullOrWhiteSpace(reportTitle) || result.PreviewCharts is null)
            return;

        foreach (var chart in result.PreviewCharts)
        {
            if (string.IsNullOrWhiteSpace(chart.AriaLabel))
                continue;

            if (chart.AriaLabel.EndsWith(" за период", StringComparison.Ordinal))
                chart.AriaLabel = $"{reportTitle} за период";
            else if (chart.AriaLabel.EndsWith(" по дням", StringComparison.Ordinal))
                chart.AriaLabel = $"{reportTitle} по дням";
        }
    }
}
