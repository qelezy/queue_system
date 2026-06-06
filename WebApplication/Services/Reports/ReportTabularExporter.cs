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
using WebApplication.Models.Reports.Configuration;
using WebApplication.Models.Reports.Constants;
using WebApplication.Services.Reports.Catalog;

namespace WebApplication.Services.Reports;

public static partial class ReportTabularExporter
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
        
        var horizontal = portrait ? PageSizes.A4.Width : PageSizes.A4.Height;
        return horizontal - PdfPageMargin * 2f;
    }

    internal static float PdfLandscapeContentWidthPoints() => PdfContentWidth(portrait: false);

    internal static float PdfPortraitContentWidthPoints() => PdfContentWidth(portrait: true);

    private static readonly HashSet<string> CsvExcludedDetailFirstCells = new(StringComparer.Ordinal)
    {
        "Итого за период",
        "Итого за день",
        "Итого по врачам",
        "Итого по специальностям",
        "Итого по кабинетам"
    };

    private static bool IsHorizontalGroupedBarExportKind(string? kind) =>
        string.Equals(kind?.Trim(), "horizontalGroupedBar", StringComparison.OrdinalIgnoreCase);

    private static bool IsLineChartExportKind(string? kind) =>
        string.Equals(kind?.Trim(), "line", StringComparison.OrdinalIgnoreCase);

    private static bool ChartExportUsesFullWidth(string? kind)
    {
        var k = kind?.Trim();
        return string.Equals(k, "groupedBar", StringComparison.OrdinalIgnoreCase)
               || IsHorizontalGroupedBarExportKind(k)
               || IsLineChartExportKind(k);
    }

    private static float PdfPieChartHeightFor(int chartCount) =>
        chartCount >= 2 ? PdfPieChartHeightWhenPair : PdfPieChartHeight;

    private static float PdfGroupedBarChartHeightFor(
        int chartCount,
        int maxGroupedBarSeriesCount = 0,
        int maxCategoryLabelCount = 0)
    {
        var baseHeight = chartCount >= 2 ? PdfGroupedBarChartHeightWhenPair : PdfGroupedBarChartHeight;
        if (maxGroupedBarSeriesCount <= 12 && maxCategoryLabelCount <= 8)
            return Math.Min(baseHeight, 400f);

        var scaled = baseHeight;
        if (maxGroupedBarSeriesCount > 12)
            scaled += (maxGroupedBarSeriesCount - 12) * 6f;
        if (maxCategoryLabelCount > 8)
            scaled += (maxCategoryLabelCount - 8) * 28f;

        return Math.Min(scaled, 520f);
    }

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
            ReportDetailRowKinds.StagesAndWaiting => IsStagesAndWaitingDetailDataRow,
            ReportDetailRowKinds.ArrivedCompleted => IsDateGroupedDetailDataRow,
            ReportDetailRowKinds.WaitingBeforeAppointment => IsDateGroupedDetailDataRow,
            ReportDetailRowKinds.AppointmentDuration => IsAppointmentDurationDetailDataRow,
            _ => IsDateGroupedDetailDataRow
        };

    public static (byte[] Bytes, string ContentType, string FileName) Export(
        ReportResultViewModel result,
        string format,
        ReportGenerateRequest? requestForHeader = null,
        ReportGeneratorKind? generatorKind = null)
    {
        var ext = NormalizeFormat(format);
        var baseName = GetBaseFileName(result);
        var headerLines = ResolveExportHeaderLines(requestForHeader, generatorKind);
        return ext switch
        {
            "xlsx" => (
                WriteXlsxBytes(result, headerLines),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                baseName + ".xlsx"),
            "pdf" => (
                WritePdfBytes(result, headerLines),
                "application/pdf",
                baseName + ".pdf"),
            "html" => (
                WriteHtmlBytes(result, headerLines),
                "text/html; charset=utf-8",
                baseName + ".html"),
            _ => (
                WriteCsvBytes(result),
                "text/csv; charset=utf-8",
                baseName + ".csv")
        };
    }

    internal static IReadOnlyList<string> ResolveExportHeaderLines(
        ReportGenerateRequest? requestForHeader,
        ReportGeneratorKind? generatorKind)
    {
        if (requestForHeader is null)
            return [];

        return generatorKind is not null
            ? ReportsUiConfiguration.FormatExportHeaderLines(generatorKind.Value, requestForHeader)
            : [];
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

    private const string NoDataLabel = "Нет данных";

    private static bool HasNoReportRows(ReportResultViewModel result) => result.Rows.Count == 0;

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

    internal static bool IsDurationColumnHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return false;

        var h = header.Trim();
        if (h.Contains('%', StringComparison.Ordinal))
            return false;

        if (ContainsIgnoreCase(h, "интервал")
            || ContainsIgnoreCase(h, "этапов")
            || ContainsIgnoreCase(h, "превышен")
            || ContainsIgnoreCase(h, "завершённ")
            || ContainsIgnoreCase(h, "приёмов")
            || ContainsIgnoreCase(h, "число интервалов"))
            return false;

        if (ContainsIgnoreCase(h, "длительн")
            || ContainsIgnoreCase(h, "ожидан")
            || ContainsIgnoreCase(h, "норматив")
            || ContainsIgnoreCase(h, "время")
            || ContainsIgnoreCase(h, "задерж")
            || ContainsIgnoreCase(h, "быстрее")
            || ContainsIgnoreCase(h, "медленнее"))
            return true;

        if ((ContainsIgnoreCase(h, "коротк") || ContainsIgnoreCase(h, "длинн"))
            && ContainsIgnoreCase(h, "приём"))
            return true;

        if (ContainsIgnoreCase(h, "наименьш") || ContainsIgnoreCase(h, "наибольш"))
        {
            return ContainsIgnoreCase(h, "ожидан")
                || ContainsIgnoreCase(h, "длительн")
                || ContainsIgnoreCase(h, "задерж");
        }

        return false;
    }

    private static bool ContainsIgnoreCase(string text, string value) =>
        text.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string FormatCellForCsv(
        IReadOnlyList<string> headers,
        int columnIndex,
        string? cell)
    {
        if (columnIndex >= headers.Count || !IsDurationColumnHeader(headers[columnIndex]))
            return cell ?? "";

        if (string.IsNullOrWhiteSpace(cell) || cell.Trim() == "—")
            return "";

        return CatalogReportShared.TryParseFormattedDurationToMinutes(cell, out var minutes)
            ? CatalogReportShared.FormatMinutesForCsv(minutes)
            : cell;
    }

}
