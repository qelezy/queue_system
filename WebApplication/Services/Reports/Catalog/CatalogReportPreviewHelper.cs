namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogReportPreviewHelper
{
    private const int PreviewTailReserved = 3;
    private const string TruncatedHint = "Показаны не все строки; полный отчёт — при сохранении в файл.";
    private const string FullDataTotalsLabel = "Итого (по полным данным)";

    internal static void ApplyDetailPreviewAndTotals<TAgg>(
        ReportResultViewModel model,
        List<ReportResultRowViewModel> detailRows,
        List<TAgg> detailData,
        ReportGenerationPurpose purpose,
        Action<List<ReportResultRowViewModel>, List<TAgg>> appendPeriodTotals,
        Func<List<TAgg>, string, IEnumerable<ReportResultRowViewModel>> buildTotalsBlockRows,
        int truncatedHintColumnCount = 6)
    {
        if (purpose != ReportGenerationPurpose.JsonPreview && detailData.Count > 0)
        {
            appendPeriodTotals(model.Rows, detailData);
            return;
        }

        if (purpose == ReportGenerationPurpose.JsonPreview
            && detailRows.Count > ReportPreviewLimits.MaxTableRows)
        {
            var maxDetail = Math.Max(0, ReportPreviewLimits.MaxTableRows - PreviewTailReserved);
            model.PreviewRowsTotal = detailRows.Count;
            model.PreviewRowLimit = ReportPreviewLimits.MaxTableRows;

            var hintCells = new string[truncatedHintColumnCount];
            hintCells[0] = "…";
            hintCells[1] = TruncatedHint;
            for (var i = 2; i < truncatedHintColumnCount; i++)
                hintCells[i] = string.Empty;

            model.Rows =
            [
                ..detailRows.Take(maxDetail),
                ReportResultRowViewModel.FromCells(
                    hintCells,
                    rowClass: "report-load-table__row--preview-truncated-hint"),
                ..buildTotalsBlockRows(detailData, FullDataTotalsLabel)
            ];
            return;
        }

        CatalogReportShared.ApplyPreviewRowCap(model, purpose);
        if (purpose == ReportGenerationPurpose.JsonPreview && detailData.Count > 0)
            appendPeriodTotals(model.Rows, detailData);
    }
}
