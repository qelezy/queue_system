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
}
