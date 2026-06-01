using WebApplication.Models.Reports.Configuration;
using WebApplication.Services.Reports;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ReportCatalogMetadataEnricherTests
{
    [Fact]
    public void ApplyToResult_replaces_chart_aria_suffix_za_period_with_report_title()
    {
        const string reportId = "test-report";
        const string reportTitle = "Задержки обслуживания";
        var catalog = new StubReportsCatalog(reportId, reportTitle);
        var enricher = new ReportCatalogMetadataEnricher(catalog);
        var result = new ReportResultViewModel
        {
            PreviewCharts =
            [
                new ReportPreviewChartDescriptor { AriaLabel = "Сводка за период" }
            ]
        };

        enricher.ApplyToResult(result, reportId);

        Assert.Equal($"{reportTitle} за период", result.PreviewCharts![0].AriaLabel);
    }

    [Fact]
    public void ApplyToResult_replaces_chart_aria_suffix_po_dnyam_with_report_title()
    {
        const string reportId = "test-report";
        const string reportTitle = "Ожидание до приёма";
        var catalog = new StubReportsCatalog(reportId, reportTitle);
        var enricher = new ReportCatalogMetadataEnricher(catalog);
        var result = new ReportResultViewModel
        {
            PreviewCharts =
            [
                new ReportPreviewChartDescriptor { AriaLabel = "Динамика по дням" }
            ]
        };

        enricher.ApplyToResult(result, reportId);

        Assert.Equal($"{reportTitle} по дням", result.PreviewCharts![0].AriaLabel);
    }

    [Fact]
    public void ApplyToResult_leaves_aria_label_when_suffix_does_not_match()
    {
        const string reportId = "test-report";
        const string originalLabel = "Среднее ожидание до вызова по дням и часам суток";
        var catalog = new StubReportsCatalog(reportId, "Ожидание до приёма");
        var enricher = new ReportCatalogMetadataEnricher(catalog);
        var result = new ReportResultViewModel
        {
            PreviewCharts =
            [
                new ReportPreviewChartDescriptor { AriaLabel = originalLabel }
            ]
        };

        enricher.ApplyToResult(result, reportId);

        Assert.Equal(originalLabel, result.PreviewCharts![0].AriaLabel);
    }

    private sealed class StubReportsCatalog(string reportId, string title) : IReportsCatalog
    {
        private readonly ReportCatalogItemViewModel _item = new()
        {
            Id = reportId,
            Title = title,
            TableLayout = ReportTableLayouts.Standard,
            PdfOrientation = ReportPdfOrientations.Portrait,
            DetailRowKind = ReportDetailRowKinds.Standard,
            GeneratorKind = ReportGeneratorKind.ServiceDelays
        };

        public IReadOnlyList<ReportCatalogItemViewModel> GetCatalog() => [_item];

        public IReadOnlyList<ReportCategoryViewModel> GetCatalogByCategory() => [];

        public bool TryGetItem(string? id, out ReportCatalogItemViewModel? item)
        {
            if (string.Equals(id, reportId, StringComparison.OrdinalIgnoreCase))
            {
                item = _item;
                return true;
            }

            item = null;
            return false;
        }

        public bool TryGetByGeneratorKind(ReportGeneratorKind kind, out ReportCatalogItemViewModel? item)
        {
            item = _item.GeneratorKind == kind ? _item : null;
            return item is not null;
        }

        public IReadOnlyList<string> GetIdsWithTableLayout(string tableLayout) =>
            string.Equals(_item.TableLayout, tableLayout, StringComparison.OrdinalIgnoreCase)
                ? [reportId]
                : [];

        public bool UsesDateRowspan(string? id) => false;

        public bool UsesPortraitPdf(string? id) => true;

        public string? GetDetailRowKind(string? id) =>
            string.Equals(id, reportId, StringComparison.OrdinalIgnoreCase) ? _item.DetailRowKind : null;

        public IReadOnlyDictionary<string, string> GetTableLayoutByReportId() =>
            new Dictionary<string, string> { [reportId] = _item.TableLayout };

        public IReadOnlyDictionary<string, string> GetDetailRowKindByReportId() =>
            new Dictionary<string, string> { [reportId] = _item.DetailRowKind };
    }
}
