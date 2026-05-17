using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
namespace WebApplication.Services.Reports;

public static partial class ReportTabularExporter
{
    public static byte[] WritePdfBytes(ReportResultViewModel result, ReportGenerateRequest? requestForPeriod = null)
    {
        EnsurePdfFontRegistered();

        var colCount = result.ColumnHeaders.Count;
        var headers = result.ColumnHeaders.ToList();
        var title = string.IsNullOrWhiteSpace(result.Title) ? "Отчёт" : result.Title;
        var periodLine = requestForPeriod is not null ? FormatPeriodForPdf(requestForPeriod) : null;

        var chartDescriptors = ReportExportChartRenderer.GetDescriptors(result);
        var chartSvgs = ReportExportChartRenderer.RenderChartSvgs(result);
        var chartCount = chartSvgs.Count;
        var pieHeight = PdfPieChartHeightFor(chartCount);
        var groupedBarHeight = PdfGroupedBarChartHeightFor(chartCount);
        var portrait = PdfUsesPortraitOrientation(result);
        var pdfContentWidth = PdfContentWidth(portrait);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(portrait ? PageSizes.A4 : PageSizes.A4.Landscape());
                page.Margin(PdfPageMargin);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily(PdfFontFamily));

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Width(pdfContentWidth).Text(title).FontSize(12).SemiBold();
                    if (!string.IsNullOrWhiteSpace(periodLine))
                        col.Item().Width(pdfContentWidth).Text(periodLine).FontSize(9);
                    for (var ci = 0; ci < chartSvgs.Count; ci++)
                    {
                        var svg = chartSvgs[ci];
                        var kind = ci < chartDescriptors.Count ? chartDescriptors[ci].Kind : null;
                        if (ChartExportUsesFullWidth(kind))
                        {
                            AppendPdfCenteredChart(col, pdfContentWidth, pdfContentWidth, groupedBarHeight, svg);
                        }
                        else
                        {
                            AppendPdfPieChart(col, pdfContentWidth, pieHeight, svg);
                        }
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (var i = 0; i < colCount; i++)
                                columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            foreach (var h in headers)
                            {
                                header.Cell().Element(HeaderCell).Text(h).SemiBold();
                            }
                        });

                        if (UsesDateRowspanTable(result))
                        {
                            AppendPdfRowspanFirstColumnTableBody(
                                table,
                                colCount,
                                result,
                                GetDetailRowPredicate(result));
                        }
                        else
                        {
                            var pdfRows = result.Rows;
                            for (var ri = 0; ri < pdfRows.Count; ri++)
                            {
                                var nextRow = ri + 1 < pdfRows.Count ? pdfRows[ri + 1] : null;
                                AppendPdfTableBodyRow(table, pdfRows[ri], colCount, nextRow);
                            }
                        }
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void AppendPdfRowspanFirstColumnTableBody(
        TableDescriptor table,
        int colCount,
        ReportResultViewModel result,
        Func<ReportResultRowViewModel, bool> isDetailDataRow)
    {
        var rows = result.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!isDetailDataRow(row) || string.IsNullOrWhiteSpace(row.Cells?[0]))
            {
                var nextRow = i + 1 < rows.Count ? rows[i + 1] : null;
                AppendPdfTableBodyRow(table, row, colCount, nextRow);
                continue;
            }

            var j = i + 1;
            while (j < rows.Count && isDetailDataRow(rows[j]) && string.IsNullOrWhiteSpace(rows[j].Cells?[0]))
                j++;

            var spanCount = j - i;
            for (var k = 0; k < spanCount; k++)
            {
                var rowIndex = i + k;
                var nextRow = rowIndex + 1 < rows.Count ? rows[rowIndex + 1] : null;
                AppendPdfLoadDowntimeDetailRow(
                    table,
                    rows[rowIndex],
                    colCount,
                    k == 0 ? spanCount : 0,
                    k > 0,
                    nextRow);
            }

            i = j - 1;
        }
    }

    private static void AppendPdfLoadDowntimeDetailRow(
        TableDescriptor table,
        ReportResultRowViewModel row,
        int colCount,
        int dateRowSpan,
        bool skipFirstCell,
        ReportResultRowViewModel? nextRow)
    {
        var cells = row.Cells ?? [];
        var colSpans = row.CellColSpans;
        if (!skipFirstCell && dateRowSpan > 0)
        {
            PdfEmitTableBodyText(
                table.Cell().RowSpan((uint)dateRowSpan),
                row,
                semiBoldLabel: false,
                cells.Count > 0 ? cells[0] ?? "" : "",
                nextRow);
        }

        var startCi = skipFirstCell || dateRowSpan > 0 ? 1 : 0;
        for (var ci = startCi; ci < cells.Count; ci++)
        {
            var span = colSpans is not null && colSpans.Count > ci ? colSpans[ci] : 1;
            if (span == 0)
                continue;
            var cell = table.Cell();
            if (span > 1)
                cell = cell.ColumnSpan((uint)span);
            PdfEmitTableBodyText(cell, row, semiBoldLabel: false, cells[ci] ?? "", nextRow);
        }
    }

    private static void AppendPdfTableBodyRow(
        TableDescriptor table,
        ReportResultRowViewModel row,
        int colCount,
        ReportResultRowViewModel? nextRow)
    {
        if (PdfRowIsTotalsStart(row))
            AppendPdfFullWidthTealSeparator(table, colCount);

        AppendPdfTableRowCells(table, row, colCount, nextRow);

        if (PdfRowIsDayTotalsEnd(row) && !PdfNextRowIsTotalsStart(nextRow))
            AppendPdfFullWidthTealSeparator(table, colCount);
    }

    private static void AppendPdfTableRowCells(
        TableDescriptor table,
        ReportResultRowViewModel row,
        int colCount,
        ReportResultRowViewModel? nextRow)
    {
        if (!RowUsesNonTrivialColSpans(row))
        {
            var padded = PadRowCells(row, colCount);
            for (var col = 0; col < padded.Count; col++)
            {
                PdfEmitTableBodyText(
                    table.Cell(),
                    row,
                    IsPdfTotalsLabelHeadingRow(row) && col == 0,
                    padded[col] ?? "",
                    nextRow);
            }

            return;
        }

        var cells = row.Cells ?? [];
        var colSpans = row.CellColSpans;
        var firstEmitted = true;
        for (var ci = 0; ci < cells.Count; ci++)
        {
            var span = colSpans is not null && colSpans.Count > ci ? colSpans[ci] : 1;
            if (span == 0)
                continue;
            var cell = table.Cell();
            if (span > 1)
                cell = cell.ColumnSpan((uint)span);
            var semi = IsPdfTotalsLabelHeadingRow(row) && firstEmitted;
            firstEmitted = false;
            PdfEmitTableBodyText(cell, row, semi, cells[ci] ?? "", nextRow);
        }
    }

    private static bool RowUsesNonTrivialColSpans(ReportResultRowViewModel row)
    {
        if (row.CellColSpans is null || row.CellColSpans.Count == 0)
            return false;
        for (var i = 0; i < row.CellColSpans.Count; i++)
        {
            var s = row.CellColSpans[i];
            if (s == 0 || s > 1)
                return true;
        }

        return false;
    }
}
