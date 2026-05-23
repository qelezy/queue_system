namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogReportPreviewHelper
{
    internal const string PeriodTotalsLabel = "Итого за период";

    /// <summary>Строки блока «Итого за период» (подпись + значения).</summary>
    private const int PreviewTotalsRowCount = 2;

    internal static void ApplyDetailPreviewAndTotals<TAgg>(
        ReportResultViewModel model,
        List<ReportResultRowViewModel> detailRows,
        List<TAgg> detailData,
        ReportGenerationPurpose purpose,
        Action<List<ReportResultRowViewModel>, List<TAgg>> appendPeriodTotals,
        Func<List<TAgg>, string, IEnumerable<ReportResultRowViewModel>> buildTotalsBlockRows)
    {
        if (purpose != ReportGenerationPurpose.JsonPreview && detailData.Count > 0)
        {
            appendPeriodTotals(model.Rows, detailData);
            return;
        }

        if (purpose == ReportGenerationPurpose.JsonPreview
            && detailRows.Count > ReportPreviewLimits.MaxTableRows)
        {
            var maxDetail = Math.Max(0, ReportPreviewLimits.MaxTableRows - PreviewTotalsRowCount);
            model.PreviewRowsTotal = detailRows.Count;
            model.PreviewRowLimit = ReportPreviewLimits.MaxTableRows;
            model.Rows =
            [
                ..detailRows.Take(maxDetail),
                ..buildTotalsBlockRows(detailData, PeriodTotalsLabel)
            ];
            return;
        }

        CatalogReportShared.ApplyPreviewRowCap(model, purpose);
        if (purpose == ReportGenerationPurpose.JsonPreview && detailData.Count > 0)
            appendPeriodTotals(model.Rows, detailData);
    }
}
