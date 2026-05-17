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
}
