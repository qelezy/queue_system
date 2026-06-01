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
    public static byte[] WriteXlsxBytes(ReportResultViewModel result, IReadOnlyList<string>? headerLines = null)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Отчёт");
        var colCount = result.ColumnHeaders.Count;
        var tableStartRow = 1;
        var exportHeaderLines = headerLines ?? [];

        if (exportHeaderLines.Count > 0 || !string.IsNullOrWhiteSpace(result.Title))
        {
            var headerRow = 1;
            var title = string.IsNullOrWhiteSpace(result.Title) ? "Отчёт" : result.Title;
            ws.Cell(headerRow, 1).Value = title;
            ws.Row(headerRow).Style.Font.Bold = true;
            headerRow++;

            foreach (var line in exportHeaderLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                ws.Cell(headerRow, 1).Value = line;
                headerRow++;
            }

            tableStartRow = headerRow;
        }

        for (var i = 0; i < colCount; i++)
            ws.Cell(tableStartRow, i + 1).Value = result.ColumnHeaders[i];

        ws.Row(tableStartRow).Style.Font.Bold = true;
        var r = tableStartRow + 1;
        if (HasNoReportRows(result))
        {
            if (colCount > 0)
            {
                ws.Cell(r, 1).Value = NoDataLabel;
                if (colCount > 1)
                    ws.Range(r, 1, r, colCount).Merge();
            }

            r++;
        }

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
}
