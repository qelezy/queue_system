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
using WebApplication.Models;

namespace WebApplication.Services.Reports;

public static class ReportTabularExporter
{
    private static readonly Regex SafeHtmlRowClassRegex = new("^[a-zA-Z0-9 _-]+$", RegexOptions.Compiled);

    private const string PdfFontFamily = "Noto Sans";
    private static readonly object FontLock = new();
    private static bool _pdfFontRegistered;

    private const string EmbeddedFontResourceSuffix = "NotoSans-Regular.ttf";
    private const string HtmlShellCssResourceSuffix = "reports-export-html.css";
    private const string SharedCssResourceSuffix = "reports-export-shared.css";

    private static string? _cachedHtmlExportCss;

    private const float PdfPageMargin = 24f;
    private const float PdfPieChartWidth = 420f;
    private const float PdfPieChartHeight = 230f;
    private const float PdfGroupedBarChartHeight = 320f;
    private const float PdfPieChartHeightWhenPair = 190f;
    private const float PdfGroupedBarChartHeightWhenPair = 260f;
    private static float PdfContentWidth(bool portrait)
    {
        // Горизонталь страницы: portrait — Width, landscape — длинная сторона A4 (Height в struct).
        var horizontal = portrait ? PageSizes.A4.Width : PageSizes.A4.Height;
        return horizontal - PdfPageMargin * 2f;
    }

    internal static float PdfLandscapeContentWidthPoints() => PdfContentWidth(portrait: false);

    private static readonly HashSet<string> CsvExcludedDetailFirstCells = new(StringComparer.Ordinal)
    {
        "Итого за период",
        "Итого (по полным данным)",
        "Итого за день",
        "Итого по врачам",
        "Итого по специальностям",
        "Итого по кабинетам",
        "…"
    };

    private static bool ChartExportUsesFullWidth(string? kind) =>
        string.Equals(kind?.Trim(), "groupedBar", StringComparison.OrdinalIgnoreCase);

    private static float PdfPieChartHeightFor(int chartCount) =>
        chartCount >= 2 ? PdfPieChartHeightWhenPair : PdfPieChartHeight;

    private static float PdfGroupedBarChartHeightFor(int chartCount) =>
        chartCount >= 2 ? PdfGroupedBarChartHeightWhenPair : PdfGroupedBarChartHeight;

    private static void AppendPdfPieChart(
        ColumnDescriptor col,
        float contentWidth,
        float chartHeight,
        string svg)
    {
        col.Item()
            .Width(contentWidth)
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(PdfPieChartWidth);
                    columns.RelativeColumn();
                });

                table.Cell();
                table.Cell()
                    .Height(chartHeight)
                    .AlignCenter()
                    .AlignMiddle()
                    .Svg(svg)
                    .FitArea();
                table.Cell();
            });
    }

    private static void AppendPdfCenteredChart(
        ColumnDescriptor col,
        float contentWidth,
        float chartWidth,
        float chartHeight,
        string svg)
    {
        col.Item()
            .Width(contentWidth)
            .Height(chartHeight)
            .AlignCenter()
            .AlignMiddle()
            .Svg(svg)
            .FitArea();
    }

    private static bool PdfUsesPortraitOrientation(ReportResultViewModel result) =>
        string.Equals(result.PdfOrientation, ReportPdfOrientations.Portrait, StringComparison.OrdinalIgnoreCase);

    private static bool UsesDateRowspanTable(ReportResultViewModel result) =>
        string.Equals(result.TableLayout, ReportTableLayouts.DateRowspan, StringComparison.OrdinalIgnoreCase);

    private static Func<ReportResultRowViewModel, bool> GetDetailRowPredicate(ReportResultViewModel result) =>
        result.DetailRowKind switch
        {
            ReportDetailRowKinds.LoadDowntime => IsLoadDowntimeDetailDataRow,
            ReportDetailRowKinds.RouteAndPauses => IsRouteAndPausesDetailDataRow,
            ReportDetailRowKinds.ArrivedCompleted => IsDateGroupedDetailDataRow,
            ReportDetailRowKinds.AppointmentDuration => IsAppointmentDurationDetailDataRow,
            _ => IsDateGroupedDetailDataRow
        };

    /// <summary>CSV: только строки детализации; без итогов, подсказок превью и пустых ячеек даты (дата дублируется в каждой строке).</summary>
    public static byte[] WriteCsvBytes(ReportResultViewModel result)
    {
        using var mem = new MemoryStream();
        using (var textWriter = new StreamWriter(mem, new UTF8Encoding(true), bufferSize: 65536, leaveOpen: true))
        {
            var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                NewLine = Environment.NewLine
            };
            using var csv = new CsvWriter(textWriter, cfg);
            foreach (var h in result.ColumnHeaders)
                csv.WriteField(h);
            csv.NextRecord();

            var colCount = result.ColumnHeaders.Count;
            string? lastDetailDate = null;
            foreach (var row in result.Rows.Where(IsCsvDetailRow))
            {
                var cells = PadRowCells(row, colCount).ToList();
                if (colCount > 0)
                {
                    var d0 = cells[0]?.Trim() ?? "";
                    if (string.IsNullOrEmpty(d0) && !string.IsNullOrEmpty(lastDetailDate))
                        cells[0] = lastDetailDate;
                    else if (!string.IsNullOrEmpty(d0))
                        lastDetailDate = d0;
                }

                foreach (var cell in cells)
                    csv.WriteField(cell);
                csv.NextRecord();
            }

            textWriter.Flush();
        }

        return mem.ToArray();
    }

    private static bool IsCsvDetailRow(ReportResultRowViewModel row)
    {
        if (IsCsvTotalsOrHintRow(row))
            return false;

        if (!string.IsNullOrWhiteSpace(row.RowClass))
            return false;

        var first = row.Cells is { Count: > 0 } ? row.Cells[0]?.Trim() ?? "" : "";
        return first.Length == 0 || !CsvExcludedDetailFirstCells.Contains(first);
    }

    private static bool IsCsvTotalsOrHintRow(ReportResultRowViewModel row)
    {
        var rc = row.RowClass ?? "";
        if (string.IsNullOrWhiteSpace(rc))
            return false;

        return rc.Contains("preview-truncated-hint", StringComparison.OrdinalIgnoreCase)
            || rc.Contains("period-total", StringComparison.OrdinalIgnoreCase)
            || rc.Contains("totals-start", StringComparison.OrdinalIgnoreCase)
            || rc.Contains("day-totals-heading", StringComparison.OrdinalIgnoreCase)
            || rc.Contains("day-totals-end", StringComparison.OrdinalIgnoreCase)
            || rc.Contains("day-doctor-total", StringComparison.OrdinalIgnoreCase);
    }

    public static byte[] WriteXlsxBytes(ReportResultViewModel result)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Отчёт");
        var colCount = result.ColumnHeaders.Count;
        const int tableStartRow = 1;

        for (var i = 0; i < colCount; i++)
            ws.Cell(tableStartRow, i + 1).Value = result.ColumnHeaders[i];

        ws.Row(tableStartRow).Style.Font.Bold = true;
        var r = tableStartRow + 1;
        foreach (var row in result.Rows)
        {
            var cells = PadRowCells(row, colCount);
            for (var c = 0; c < colCount; c++)
                ws.Cell(r, c + 1).Value = cells[c];
            r++;
        }

        ws.SheetView.FreezeRows(tableStartRow);
        ws.Columns(1, colCount).AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] WriteHtmlBytes(ReportResultViewModel result, ReportGenerateRequest? requestForPeriod = null)
    {
        var title = string.IsNullOrWhiteSpace(result.Title) ? "Отчёт" : result.Title;
        var periodLine = requestForPeriod is not null ? FormatPeriodForPdf(requestForPeriod) : null;
        var chartDescriptors = ReportExportChartRenderer.GetDescriptors(result);
        var chartSvgs = ReportExportChartRenderer.RenderChartSvgs(result);

        var sb = new StringBuilder(1 << 16);
        sb.Append("<!DOCTYPE html>\n<html lang=\"ru\">\n<head>\n<meta charset=\"utf-8\">\n<title>")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</title>\n<style>\n")
            .Append(GetHtmlExportCss())
            .Append("\n</style>\n</head>\n<body class=\"report-export-html\">\n<div class=\"report-export-html__inner\">\n")
            .Append("<h1 class=\"report-export-html__title\">")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</h1>\n");

        if (!string.IsNullOrWhiteSpace(periodLine))
        {
            sb.Append("<p class=\"report-export-html__period\">")
                .Append(WebUtility.HtmlEncode(periodLine))
                .Append("</p>\n");
        }

        for (var ci = 0; ci < chartSvgs.Count; ci++)
        {
            var kind = ci < chartDescriptors.Count ? chartDescriptors[ci].Kind : null;
            var wrapClass = ChartExportUsesFullWidth(kind)
                ? "report-preview-modal__chart-wrap report-preview-modal__chart-wrap--grouped-bar"
                : "report-preview-modal__chart-wrap";
            sb.Append("<div class=\"").Append(wrapClass).Append("\" role=\"presentation\">")
                .Append(chartSvgs[ci])
                .Append("</div>\n");
        }

        sb.Append("<div class=\"report-preview-modal__table-wrap\"><div class=\"users-table-wrap report-preview-table\"><table class=\"users-table users-table--report-preview\"><thead><tr>\n");
        foreach (var h in result.ColumnHeaders)
            sb.Append("<th>").Append(WebUtility.HtmlEncode(h)).Append("</th>\n");

        sb.Append("</tr></thead><tbody>\n");
        AppendHtmlTableBody(sb, result);
        sb.Append("</tbody></table></div></div>\n</div>\n</body>\n</html>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string GetHtmlExportCss()
    {
        if (_cachedHtmlExportCss is not null)
            return _cachedHtmlExportCss;

        var shell = LoadEmbeddedTextResource(HtmlShellCssResourceSuffix);
        var shared = LoadEmbeddedTextResource(SharedCssResourceSuffix);
        _cachedHtmlExportCss = shell + "\n" + shared;
        return _cachedHtmlExportCss;
    }

    private static string LoadEmbeddedTextResource(string nameSuffix)
    {
        var asm = typeof(ReportTabularExporter).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(nameSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Не найден встроенный ресурс: " + nameSuffix);

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Не удалось открыть ресурс: " + resourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void AppendHtmlTableBody(StringBuilder sb, ReportResultViewModel result)
    {
        if (!UsesDateRowspanTable(result))
        {
            foreach (var row in result.Rows)
                AppendHtmlTableRowStandard(sb, row);
            return;
        }

        AppendHtmlRowspanFirstColumnBody(sb, result, GetDetailRowPredicate(result));
    }

    /// <summary>Группировка подряд идущих строк с пустой первой ячейкой: rowspan по колонке даты.</summary>
    private static void AppendHtmlRowspanFirstColumnBody(
        StringBuilder sb,
        ReportResultViewModel result,
        Func<ReportResultRowViewModel, bool> isDetailDataRow)
    {
        var rows = result.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!isDetailDataRow(row) || string.IsNullOrWhiteSpace(row.Cells?[0]))
            {
                AppendHtmlTableRowStandard(sb, row);
                continue;
            }

            var j = i + 1;
            while (j < rows.Count && isDetailDataRow(rows[j]) && string.IsNullOrWhiteSpace(rows[j].Cells?[0]))
                j++;

            var spanCount = j - i;
            for (var k = 0; k < spanCount; k++)
                AppendHtmlTableRowLoadDowntimeDetail(sb, rows[i + k], k == 0 ? spanCount : 0, k > 0);

            i = j - 1;
        }
    }

    private static bool IsArrivedCompletedDetailDataRow(ReportResultRowViewModel row) =>
        IsDateGroupedDetailDataRow(row);

    private static bool IsDateGroupedDetailDataRow(ReportResultRowViewModel row)
    {
        if (!string.IsNullOrWhiteSpace(row.RowClass))
            return false;
        var cells = row.Cells;
        if (cells is not { Count: >= 6 })
            return false;
        var c0 = cells[0]?.Trim() ?? "";
        if (c0 is "Итого за период" or "Итого (по полным данным)" or "…")
            return false;
        return int.TryParse(
            cells[2]?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out _);
    }

    private static bool IsRouteAndPausesDetailDataRow(ReportResultRowViewModel row)
    {
        if (!string.IsNullOrWhiteSpace(row.RowClass))
            return false;
        var cells = row.Cells;
        if (cells is not { Count: >= 6 })
            return false;
        var c0 = cells[0]?.Trim() ?? "";
        if (c0 is "Итого за период" or "Итого (по полным данным)" or "…")
            return false;
        return int.TryParse(
            cells[3]?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out _);
    }

    private static bool IsLoadDowntimeDetailDataRow(ReportResultRowViewModel row)
    {
        if (!string.IsNullOrWhiteSpace(row.RowClass))
            return false;
        var cells = row.Cells;
        return cells is { Count: >= 2 } && cells[1] != "—";
    }

    private static bool IsAppointmentDurationDetailDataRow(ReportResultRowViewModel row)
    {
        if (!string.IsNullOrWhiteSpace(row.RowClass))
            return false;
        var cells = row.Cells;
        if (cells is not { Count: >= 8 })
            return false;
        var c0 = cells[0]?.Trim() ?? "";
        if (c0 is "Итого за период" or "Итого (по полным данным)" or "…" or "Итого за день")
            return false;
        var countIdx = cells.Count >= 9 ? 3 : 2;
        return int.TryParse(
            cells[countIdx]?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out _);
    }

    private static void AppendHtmlTableRowStandard(StringBuilder sb, ReportResultRowViewModel row)
    {
        var rc = row.RowClass;
        var classAttr = rc is not null && SafeHtmlRowClassRegex.IsMatch(rc)
            ? " class=\"" + WebUtility.HtmlEncode(rc) + "\""
            : "";
        sb.Append("<tr").Append(classAttr).Append(">\n");
        var cells = row.Cells ?? [];
        var colSpans = row.CellColSpans;
        for (var ci = 0; ci < cells.Count; ci++)
        {
            var span = colSpans is not null && colSpans.Count > ci ? colSpans[ci] : 1;
            if (span == 0)
                continue;
            var attr = span > 1 ? " colspan=\"" + span.ToString(CultureInfo.InvariantCulture) + "\"" : "";
            sb.Append("<td").Append(attr).Append(">")
                .Append(WebUtility.HtmlEncode(cells[ci] ?? ""))
                .Append("</td>\n");
        }

        sb.Append("</tr>\n");
    }

    private static void AppendHtmlTableRowLoadDowntimeDetail(
        StringBuilder sb,
        ReportResultRowViewModel row,
        int dateRowspan,
        bool skipFirstCell)
    {
        sb.Append("<tr>\n");
        var cells = row.Cells ?? [];
        var colSpans = row.CellColSpans;
        var colCount = cells.Count;

        if (!skipFirstCell && dateRowspan > 0)
        {
            sb.Append("<td rowspan=\"").Append(dateRowspan.ToString(CultureInfo.InvariantCulture)).Append("\">")
                .Append(WebUtility.HtmlEncode(cells.Count > 0 ? cells[0] ?? "" : ""))
                .Append("</td>\n");
        }

        var startCi = skipFirstCell || dateRowspan > 0 ? 1 : 0;
        for (var ci = startCi; ci < colCount; ci++)
        {
            var span = colSpans is not null && colSpans.Count > ci ? colSpans[ci] : 1;
            if (span == 0)
                continue;
            var attr = span > 1 ? " colspan=\"" + span.ToString(CultureInfo.InvariantCulture) + "\"" : "";
            sb.Append("<td").Append(attr).Append(">")
                .Append(WebUtility.HtmlEncode(cells[ci] ?? ""))
                .Append("</td>\n");
        }

        sb.Append("</tr>\n");
    }

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

    public static (byte[] Bytes, string ContentType, string FileName) Export(
        ReportResultViewModel result,
        string format,
        ReportGenerateRequest? requestForPeriod = null)
    {
        var ext = NormalizeFormat(format);
        var baseName = GetBaseFileName(result);
        return ext switch
        {
            "xlsx" => (
                WriteXlsxBytes(result),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                baseName + ".xlsx"),
            "pdf" => (
                WritePdfBytes(result, requestForPeriod),
                "application/pdf",
                baseName + ".pdf"),
            "html" => (
                WriteHtmlBytes(result, requestForPeriod),
                "text/html; charset=utf-8",
                baseName + ".html"),
            _ => (
                WriteCsvBytes(result),
                "text/csv; charset=utf-8",
                baseName + ".csv")
        };
    }

    private static string? FormatPeriodForPdf(ReportGenerateRequest r)
    {
        var d0 = r.DateFrom?.Trim();
        var d1 = r.DateTo?.Trim();
        if (string.IsNullOrEmpty(d0) && string.IsNullOrEmpty(d1))
        {
            if (!string.IsNullOrWhiteSpace(r.WeekStart)
                && DateTime.TryParse(r.WeekStart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var mon))
            {
                var end = mon.Date.AddDays(6);
                return "Период (неделя): " + mon.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"))
                    + " — " + end.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
            }

            return null;
        }

        static string Part(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "…";
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
            return s;
        }

        return "Период: " + Part(d0) + " — " + Part(d1);
    }

    private static string NormalizeFormat(string? format)
    {
        var f = (format ?? "csv").Trim().ToLowerInvariant();
        return f is "xlsx" or "pdf" or "html" ? f : "csv";
    }

    private static string GetBaseFileName(ReportResultViewModel result)
    {
        var name = result.DownloadFileName?.Trim();
        if (string.IsNullOrEmpty(name))
            return string.IsNullOrWhiteSpace(result.GeneratedForReportId)
                ? "report"
                : Path.GetFileNameWithoutExtension(result.GeneratedForReportId.Trim());
        var ext = Path.GetExtension(name);
        return string.IsNullOrEmpty(ext) ? name : Path.GetFileNameWithoutExtension(name);
    }

    private static IReadOnlyList<string> PadRowCells(ReportResultRowViewModel row, int colCount)
    {
        var cells = row.Cells ?? [];
        if (cells.Count >= colCount)
            return cells.Take(colCount).ToList();

        var list = new List<string>(colCount);
        list.AddRange(cells);
        while (list.Count < colCount)
            list.Add("");
        return list;
    }

    private static void EnsurePdfFontRegistered()
    {
        if (Volatile.Read(ref _pdfFontRegistered))
            return;

        lock (FontLock)
        {
            if (_pdfFontRegistered)
                return;

            var asm = typeof(ReportTabularExporter).Assembly;
            var resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(EmbeddedFontResourceSuffix, StringComparison.Ordinal));
            if (resourceName is null)
                throw new InvalidOperationException("Не найден встроенный шрифт для PDF: " + EmbeddedFontResourceSuffix);

            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("Не удалось открыть ресурс шрифта: " + resourceName);
            FontManager.RegisterFont(stream);
            _pdfFontRegistered = true;
        }
    }

    private static readonly Color PdfTotalsSeparatorColor = Colors.Teal.Lighten2;

    private static bool PdfRowIsDayTotalsEnd(ReportResultRowViewModel row) =>
        (row.RowClass ?? "").Contains("day-totals-end", StringComparison.OrdinalIgnoreCase);

    private static bool PdfRowIsTotalsStart(ReportResultRowViewModel row) =>
        (row.RowClass ?? "").Contains("totals-start", StringComparison.OrdinalIgnoreCase);

    private static void AppendPdfFullWidthTealSeparator(TableDescriptor table, int colCount)
    {
        table.Cell().ColumnSpan((uint)colCount).Element(c =>
            c.Padding(0)
                .BorderBottom(0.5f)
                .BorderColor(PdfTotalsSeparatorColor)
                .DefaultTextStyle(x => x.FontSize(1))
                .Text(""));
    }

    private static bool IsPdfTotalsLabelHeadingRow(ReportResultRowViewModel row)
    {
        var rc = row.RowClass ?? "";
        return rc.Contains("totals-start", StringComparison.OrdinalIgnoreCase)
            || rc.Contains("day-totals-heading", StringComparison.OrdinalIgnoreCase)
            || rc.Contains("period-total", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PdfNextRowIsTotalsStart(ReportResultRowViewModel? nextRow) =>
        (nextRow?.RowClass ?? "").Contains("totals-start", StringComparison.OrdinalIgnoreCase);

    private static bool PdfSuppressBottomBorder(ReportResultRowViewModel row, ReportResultRowViewModel? nextRow) =>
        PdfNextRowIsTotalsStart(nextRow) || PdfRowIsDayTotalsEnd(row);

    private static IContainer PdfTableBodyCell(IContainer container, bool suppressBottom)
    {
        if (suppressBottom)
            return container.PaddingVertical(0).PaddingHorizontal(3);

        return container
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(2)
            .PaddingHorizontal(3);
    }

    private static void PdfEmitTableBodyText(
        IContainer tableCell,
        ReportResultRowViewModel row,
        bool semiBoldLabel,
        string text,
        ReportResultRowViewModel? nextRow = null)
    {
        var suppressBottom = PdfSuppressBottomBorder(row, nextRow);
        var inner = tableCell.Element(c => PdfTableBodyCell(c, suppressBottom));
        (semiBoldLabel ? inner.DefaultTextStyle(x => x.SemiBold()) : inner).Text(text);
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Medium)
            .PaddingVertical(4)
            .PaddingHorizontal(3);

}
