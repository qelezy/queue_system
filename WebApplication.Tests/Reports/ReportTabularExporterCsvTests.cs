using System.Text;
using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ReportTabularExporterCsvTests
{
    [Fact]
    public void WriteCsvBytes_excludes_totals_and_includes_only_detail_rows()
    {
        var result = new ReportResultViewModel
        {
            ColumnHeaders = ["Дата", "Категория", "Кол-во"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["2026-05-10", "A", "1"]),
                ReportResultRowViewModel.FromCells(
                    ["Итого за период", "", ""],
                    rowClass: "report-load-table__row--totals-start"),
                ReportResultRowViewModel.FromCells(["", "—", "1"], rowClass: "report-load-table__row--period-total")
            ]
        };

        var text = DecodeCsv(ReportTabularExporter.WriteCsvBytes(result));
        var lines = SplitRecords(text);

        Assert.Equal(2, lines.Count);
        Assert.Contains("2026-05-10", lines[1]);
        Assert.DoesNotContain("Итого", text);
    }

    [Fact]
    public void WriteCsvBytes_fills_empty_date_from_previous_detail_row()
    {
        var result = new ReportResultViewModel
        {
            ColumnHeaders = ["Дата", "Интервал", "Кол-во"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["10-05-2026", "08:00–09:00", "3"]),
                ReportResultRowViewModel.FromCells(["", "09:00–10:00", "2"])
            ]
        };

        var text = DecodeCsv(ReportTabularExporter.WriteCsvBytes(result));
        var lines = SplitRecords(text);

        Assert.Equal(3, lines.Count);
        Assert.StartsWith("10-05-2026", lines[1]);
        Assert.StartsWith("10-05-2026", lines[2]);
    }

    [Fact]
    public void WriteCsvBytes_excludes_day_totals_heading()
    {
        var result = new ReportResultViewModel
        {
            ColumnHeaders = ["Дата", "Интервал", "Кол-во"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["10-05-2026", "08:00–09:00", "1"]),
                ReportResultRowViewModel.FromCells(
                    ["Итого за день", "", "", "", "", ""],
                    rowClass: "report-load-table__row--day-totals-heading"),
                ReportResultRowViewModel.FromCells(
                    ["", "—", "1", "", "", ""],
                    rowClass: "report-load-table__row--day-totals-end")
            ]
        };

        var text = DecodeCsv(ReportTabularExporter.WriteCsvBytes(result));
        var lines = SplitRecords(text);

        Assert.Equal(2, lines.Count);
        Assert.DoesNotContain("Итого за день", text);
    }

    [Fact]
    public void WriteHtmlBytes_embeds_totals_row_styles_with_preview_selector_specificity()
    {
        var result = new ReportResultViewModel
        {
            Title = "Тест",
            ColumnHeaders = ["Дата", "Категория"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["2026-05-10", "A"]),
                ReportResultRowViewModel.FromCells(
                    ["Итого за период", ""],
                    rowClass: "report-load-table__row--totals-start"),
                ReportResultRowViewModel.FromCells(
                    ["", "—"],
                    rowClass: "report-load-table__row--day-totals-end")
            ]
        };

        var html = Encoding.UTF8.GetString(ReportTabularExporter.WriteHtmlBytes(result));

        Assert.Contains("class=\"report-load-table__row--totals-start\"", html);
        Assert.Contains("class=\"report-load-table__row--day-totals-end\"", html);
        Assert.Contains(
            ".report-export-html .report-preview-modal__table-wrap .users-table tr.report-load-table__row--totals-start td",
            html);
        Assert.Contains(
            ".report-export-html .report-preview-modal__table-wrap .users-table tr.report-load-table__row--day-totals-end td",
            html);
        Assert.Contains("border-top: 1px solid rgba(0, 179, 184, 0.35)", html);
        Assert.Contains("border-bottom: 1px solid rgba(0, 179, 184, 0.35)", html);
    }

    [Fact]
    public void WriteHtmlBytes_embeds_export_table_and_chart_style_overrides()
    {
        var descriptor = new ReportPreviewChartDescriptor
        {
            Kind = "groupedBar",
            Labels = ["2026-05-28"],
            ValueUnit = "мин",
            Datasets =
            [
                new ReportPreviewChartDataset { Label = "08:00", Values = [5] }
            ]
        };

        var result = new ReportResultViewModel
        {
            Title = "Тест",
            ColumnHeaders = ["Дата", "Значение"],
            Rows = [ReportResultRowViewModel.FromCells(["2026-05-28", "5"])],
            PreviewCharts = [descriptor]
        };

        var html = Encoding.UTF8.GetString(ReportTabularExporter.WriteHtmlBytes(result));

        Assert.Contains(".report-export-html .users-table.users-table--report-preview thead th", html);
        Assert.Contains("text-transform: uppercase", html);
        Assert.Contains("color: #555555", html);
        Assert.Contains("font-size: clamp(10px, 0.65vw + 9px, 11px)", html);
        Assert.Contains(".report-export-html .report-preview-modal__chart-wrap svg", html);
        Assert.Contains("max-height: 600px", html);
        Assert.Contains("width=\"100%\" height=\"auto\"", html);
    }

    [Fact]
    public void Embedded_export_css_includes_preview_and_html_prefixes()
    {
        var asm = typeof(ReportTabularExporter).Assembly;
        var sharedName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("reports-export-shared.css", StringComparison.Ordinal));
        Assert.NotNull(sharedName);

        using var stream = asm.GetManifestResourceStream(sharedName!);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        var css = reader.ReadToEnd();

        Assert.Contains(".report-preview-modal__table-wrap", css);
        Assert.Contains(".report-export-html", css);
        Assert.Contains("report-load-table__row--totals-start", css);
    }

    [Fact]
    public void WriteCsvBytes_prefers_csv_cells_over_display_duration_parse()
    {
        var result = new ReportResultViewModel
        {
            ColumnHeaders = ["Дата", "Средняя длительность"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(
                    ["2026-05-10", "49 с"],
                    csvCells: ["2026-05-10", "0.83"])
            ]
        };

        var text = DecodeCsv(ReportTabularExporter.WriteCsvBytes(result));

        Assert.Contains("0.83", text);
        Assert.DoesNotContain("0.82", text);
    }

    [Theory]
    [InlineData("49 с", "0.82")]
    [InlineData("4 мин", "4.00")]
    [InlineData("1 ч 30 мин", "90.00")]
    [InlineData("—", "")]
    public void WriteCsvBytes_converts_duration_columns_to_decimal_minutes(string cell, string expectedCsv)
    {
        var result = new ReportResultViewModel
        {
            ColumnHeaders = ["Дата", "Средняя длительность", "Приёмов"],
            Rows = [ReportResultRowViewModel.FromCells(["2026-05-10", cell, "15"])]
        };

        var text = DecodeCsv(ReportTabularExporter.WriteCsvBytes(result));
        var dataLine = SplitRecords(text)[1];

        Assert.Contains(expectedCsv, dataLine);
        Assert.Contains(",15", dataLine);
    }

    [Fact]
    public void WriteCsvBytes_leaves_count_columns_unconverted()
    {
        var result = new ReportResultViewModel
        {
            ColumnHeaders = ["Категория", "Приёмов", "Среднее ожидание"],
            Rows = [ReportResultRowViewModel.FromCells(["ОМС", "15", "4 мин"])]
        };

        var text = DecodeCsv(ReportTabularExporter.WriteCsvBytes(result));
        var dataLine = SplitRecords(text)[1];

        Assert.Contains(",15,", dataLine);
        Assert.Contains("4.00", dataLine);
    }

    [Fact]
    public void WriteCsvBytes_excludes_preview_truncated_hint()
    {
        var result = new ReportResultViewModel
        {
            ColumnHeaders = ["Дата", "Значение"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["2026-05-10", "1"]),
                ReportResultRowViewModel.FromCells(
                    ["…", "Показаны не все строки"],
                    rowClass: "report-load-table__row--preview-truncated-hint")
            ]
        };

        var text = DecodeCsv(ReportTabularExporter.WriteCsvBytes(result));
        var lines = SplitRecords(text);

        Assert.Equal(2, lines.Count);
        Assert.DoesNotContain("Показаны не все", text);
    }

    private static string DecodeCsv(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');

    private static List<string> SplitRecords(string text) =>
        text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).ToList();
}
