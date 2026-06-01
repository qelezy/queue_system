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
    public static byte[] WriteHtmlBytes(ReportResultViewModel result, IReadOnlyList<string>? headerLines = null)
    {
        var title = string.IsNullOrWhiteSpace(result.Title) ? "Отчёт" : result.Title;
        var exportHeaderLines = headerLines ?? [];
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

        foreach (var headerLine in exportHeaderLines)
        {
            if (string.IsNullOrWhiteSpace(headerLine))
                continue;
            sb.Append("<p class=\"report-export-html__period\">")
                .Append(WebUtility.HtmlEncode(headerLine))
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
        if (HasNoReportRows(result))
        {
            AppendHtmlNoDataRow(sb, result.ColumnHeaders.Count);
            return;
        }

        if (!UsesDateRowspanTable(result))
        {
            foreach (var row in result.Rows)
                AppendHtmlTableRowStandard(sb, row);
            return;
        }

        AppendHtmlRowspanFirstColumnBody(sb, result, GetDetailRowPredicate(result));
    }

    private static void AppendHtmlNoDataRow(StringBuilder sb, int colCount)
    {
        var span = Math.Max(1, colCount);
        var attr = span > 1 ? " colspan=\"" + span.ToString(CultureInfo.InvariantCulture) + "\"" : "";
        sb.Append("<tr><td").Append(attr).Append(">")
            .Append(WebUtility.HtmlEncode(NoDataLabel))
            .Append("</td></tr>\n");
    }

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
        if (cells is not { Count: >= 5 })
            return false;
        var c0 = cells[0]?.Trim() ?? "";
        if (c0 is "Итого за период")
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
        if (cells is not { Count: >= 5 })
            return false;
        var c0 = cells[0]?.Trim() ?? "";
        if (c0 is "Итого за период")
            return false;
        return int.TryParse(
            cells[2]?.Trim(),
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
        if (c0 is "Итого за период" or "Итого за день")
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
}
