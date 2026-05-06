using System.Globalization;
using System.Text;
using WebApplication.Models;

namespace WebApplication.Services;

public sealed class MockReportGenerationService : IReportGenerationService
{
    public IReadOnlyList<ReportSelectOption> GetCabinetOptions() =>
        ElectronicQueueMockData.Cabinets
            .Select(c => new ReportSelectOption { Id = c.Id, Label = c.Label })
            .ToList();

    public IReadOnlyList<ReportSelectOption> GetDoctorOptions() =>
        ElectronicQueueMockData.Doctors
            .Select(d => new ReportSelectOption { Id = d.Id, Label = d.Name })
            .ToList();

    public IReadOnlyList<ReportSelectOption> GetCategoryOptions() =>
        ElectronicQueueMockData.Categories
            .Select(c => new ReportSelectOption { Id = c.Id, Label = c.Name })
            .ToList();

    public ReportGenerateResponse Generate(ReportGenerateRequest request)
    {
        var reportId = request.ReportId?.Trim() ?? "";
        if (string.Equals(reportId, ReportIds.QueueSummary, StringComparison.OrdinalIgnoreCase))
        {
            var model = new QueueSummaryReportParametersViewModel
            {
                DateFrom = string.IsNullOrWhiteSpace(request.DateFrom) ? DateTime.UtcNow.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : request.DateFrom!,
                DateTo = string.IsNullOrWhiteSpace(request.DateTo) ? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : request.DateTo!,
                CabinetId = request.CabinetId,
                DoctorId = request.DoctorId
            };
            return new ReportGenerateResponse { Success = true, Implemented = true, Result = GenerateQueueSummary(model) };
        }

        if (string.Equals(reportId, ReportIds.CabinetLoad, StringComparison.OrdinalIgnoreCase))
        {
            var model = new CabinetLoadReportParametersViewModel
            {
                WeekStart = string.IsNullOrWhiteSpace(request.WeekStart) ? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : request.WeekStart!
            };
            return new ReportGenerateResponse { Success = true, Implemented = true, Result = GenerateCabinetLoad(model) };
        }

        return new ReportGenerateResponse
        {
            Success = true,
            Implemented = false,
            Message = "Формирование выбранного отчета пока не реализовано."
        };
    }

    public (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request)
    {
        var generated = Generate(request);
        if (!generated.Implemented || generated.Result is null)
        {
            return (Encoding.UTF8.GetBytes("report;status\nnot_implemented;true\n"), "text/csv; charset=utf-8", "report-not-implemented.csv");
        }

        var format = (request.Format ?? "csv").Trim().ToLowerInvariant();
        if (format == "xlsx")
        {
            var bytes = ToCsvBytes(generated.Result);
            return (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{generated.Result.GeneratedForReportId}.xlsx");
        }

        if (format == "pdf")
        {
            var bytes = Encoding.UTF8.GetBytes("PDF export placeholder\n" + Encoding.UTF8.GetString(ToCsvBytes(generated.Result)));
            return (bytes, "application/pdf", $"{generated.Result.GeneratedForReportId}.pdf");
        }

        return (ToCsvBytes(generated.Result), "text/csv; charset=utf-8", $"{generated.Result.GeneratedForReportId}.csv");
    }

    public ReportResultViewModel GenerateQueueSummary(QueueSummaryReportParametersViewModel parameters)
    {
        var period = $"{parameters.DateFrom} — {parameters.DateTo}";
        var cab = parameters.CabinetId is null
            ? "все"
            : ElectronicQueueMockData.Cabinets.FirstOrDefault(c => c.Id == parameters.CabinetId.Value).Label ?? "—";
        var doc = parameters.DoctorId is null
            ? "все"
            : ElectronicQueueMockData.Doctors.FirstOrDefault(d => d.Id == parameters.DoctorId.Value).Name ?? "—";

        return new ReportResultViewModel
        {
            GeneratedForReportId = ReportIds.QueueSummary,
            Title = "Сводка по очереди (демо)",
            DownloadFileName = "queue-summary-demo.csv",
            ColumnHeaders = ["Показатель", "Значение", "Комментарий"],
            Rows =
            [
                new ReportResultRowViewModel { Cells = ["Период", period, "демо, БД очереди недоступна"] },
                new ReportResultRowViewModel { Cells = ["Кабинет", cab, "фильтр"] },
                new ReportResultRowViewModel { Cells = ["Врач", doc, "фильтр"] },
                new ReportResultRowViewModel
                {
                    Cells = ["Завершённых талонов (уникальных)", "42", "демо-значение"]
                },
                new ReportResultRowViewModel
                {
                    Cells = ["Среднее ожидание до вызова, мин", "18,5", "демо-значение"]
                },
                new ReportResultRowViewModel
                {
                    Cells = ["Средняя длительность обслуживания, мин", "22,0", "демо-значение"]
                }
            ]
        };
    }

    public ReportResultViewModel GenerateCabinetLoad(CabinetLoadReportParametersViewModel parameters)
    {
        var rows = ElectronicQueueMockData.Cabinets.Select((c, i) => new ReportResultRowViewModel
        {
            Cells =
            [
                c.Label,
                (35 + i * 12 % 50).ToString(CultureInfo.InvariantCulture) + "%",
                parameters.WeekStart + " (демо)"
            ]
        }).ToList();

        return new ReportResultViewModel
        {
            GeneratedForReportId = ReportIds.CabinetLoad,
            Title = "Загрузка кабинетов (демо)",
            DownloadFileName = "cabinet-load-demo.csv",
            ColumnHeaders = ["Кабинет", "Загрузка %", "Неделя с"],
            Rows = rows
        };
    }

    public byte[] BuildMockCsv(string reportId)
    {
        if (string.Equals(reportId, ReportIds.QueueSummary, StringComparison.OrdinalIgnoreCase))
        {
            var p = new QueueSummaryReportParametersViewModel
            {
                DateFrom = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTo = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            return ToCsvBytes(GenerateQueueSummary(p));
        }

        if (string.Equals(reportId, ReportIds.CabinetLoad, StringComparison.OrdinalIgnoreCase))
        {
            var p = new CabinetLoadReportParametersViewModel
            {
                WeekStart = DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            return ToCsvBytes(GenerateCabinetLoad(p));
        }

        return Encoding.UTF8.GetBytes("reportId;status\nunknown;not_found\n");
    }

    private static byte[] ToCsvBytes(ReportResultViewModel result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", result.ColumnHeaders.Select(EscapeCsv)));
        foreach (var row in result.Rows)
            sb.AppendLine(string.Join(";", row.Cells.Select(EscapeCsv)));

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string? cell)
    {
        var s = cell ?? "";
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return s;
    }
}
