using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services;

public sealed class ReportGenerationService : IReportGenerationService
{
    private readonly ElectronicQueueDbContext _queue;

    public ReportGenerationService(ElectronicQueueDbContext queue)
    {
        _queue = queue;
    }

    public IReadOnlyList<ReportSelectOption> GetCabinetOptions() =>
        _queue.Cabinets
            .AsNoTracking()
            .OrderBy(c => c.CabinetNumber)
            .Select(c => new ReportSelectOption { Id = c.IdCabinet, Label = $"Каб. {c.CabinetNumber}" })
            .ToList();

    public IReadOnlyList<ReportSelectOption> GetDoctorOptions() =>
        _queue.Doctors
            .AsNoTracking()
            .OrderBy(d => d.FullName)
            .Select(d => new ReportSelectOption { Id = d.IdDoctor, Label = d.FullName })
            .ToList();

    public IReadOnlyList<ReportSelectOption> GetCategoryOptions() =>
        _queue.Categories
            .AsNoTracking()
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Name)
            .Select(c => new ReportSelectOption { Id = c.IdCategory, Label = c.Name })
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
            // Техническая заглушка: формат XLSX будет сформирован отдельным генератором.
            var bytes = ToCsvBytes(generated.Result);
            return (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{generated.Result.GeneratedForReportId}.xlsx");
        }

        if (format == "pdf")
        {
            // Техническая заглушка: PDF строится из тех же табличных данных.
            var bytes = Encoding.UTF8.GetBytes("PDF export placeholder\n" + Encoding.UTF8.GetString(ToCsvBytes(generated.Result)));
            return (bytes, "application/pdf", $"{generated.Result.GeneratedForReportId}.pdf");
        }

        return (ToCsvBytes(generated.Result), "text/csv; charset=utf-8", $"{generated.Result.GeneratedForReportId}.csv");
    }

    public ReportResultViewModel GenerateQueueSummary(QueueSummaryReportParametersViewModel parameters)
    {
        if (!DateTime.TryParse(
                parameters.DateFrom,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fromDate)
            || !DateTime.TryParse(
                parameters.DateTo,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var toDate))
        {
            fromDate = DateTime.UtcNow.Date.AddDays(-7);
            toDate = DateTime.UtcNow.Date;
        }

        var fromDo = DateOnly.FromDateTime(fromDate.Date);
        var toDo = DateOnly.FromDateTime(toDate.Date);

        var query =
            from li in _queue.ListItems.AsNoTracking()
            join a in _queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            where a.DateArrival >= fromDo && a.DateArrival <= toDo
                  && li.TimeEndServicing != null
                  && li.TimeStartServicing != null
                  && li.TimeCall != null
            select new { li, a };

        if (parameters.CabinetId is { } cabL)
        {
            var cabId = (int)cabL;
            query = query.Where(x => x.li.IdCabinet == cabId);
        }

        if (parameters.DoctorId is { } docL)
        {
            var docId = (int)docL;
            query = query.Where(x => x.li.IdDoctor == docId);
        }

        var rows = query
            .Select(x => new
            {
                x.a.IdAppointment,
                x.a.DateArrival,
                x.a.TimeArrival,
                Call = x.li.TimeCall!.Value,
                Start = x.li.TimeStartServicing!.Value,
                End = x.li.TimeEndServicing!.Value
            })
            .ToList();

        var completedCount = rows.Select(x => x.IdAppointment).Distinct().Count();

        var avgWait = rows.Count == 0
            ? "—"
            : Math.Round(rows.Average(x =>
                (EqDateTimeExtensions.CombineOnArrivalDate(x.DateArrival, x.Call)
                 - EqDateTimeExtensions.CombineOnArrivalDate(x.DateArrival, x.TimeArrival)).TotalMinutes), 1)
                .ToString(CultureInfo.InvariantCulture);

        var avgServe = rows.Count == 0
            ? "—"
            : Math.Round(rows.Average(x =>
                (EqDateTimeExtensions.CombineOnArrivalDate(x.DateArrival, x.End)
                 - EqDateTimeExtensions.CombineOnArrivalDate(x.DateArrival, x.Start)).TotalMinutes), 1)
                .ToString(CultureInfo.InvariantCulture);

        var period = $"{parameters.DateFrom} — {parameters.DateTo}";
        var cab = parameters.CabinetId is null
            ? "все"
            : _queue.Cabinets.AsNoTracking()
                .Where(c => c.IdCabinet == (int)parameters.CabinetId.Value)
                .Select(c => "Каб. " + c.CabinetNumber)
                .FirstOrDefault() ?? "—";
        var doc = parameters.DoctorId is null
            ? "все"
            : _queue.Doctors.AsNoTracking()
                .Where(d => d.IdDoctor == (int)parameters.DoctorId.Value)
                .Select(d => d.FullName)
                .FirstOrDefault() ?? "—";

        return new ReportResultViewModel
        {
            GeneratedForReportId = ReportIds.QueueSummary,
            Title = "Сводка по очереди",
            DownloadFileName = "queue-summary.csv",
            ColumnHeaders = new List<string> { "Показатель", "Значение", "Комментарий" },
            Rows = new List<ReportResultRowViewModel>
            {
                new() { Cells = new List<string> { "Период", period, "из параметров" } },
                new() { Cells = new List<string> { "Кабинет", cab, "фильтр" } },
                new() { Cells = new List<string> { "Врач", doc, "фильтр" } },
                new()
                {
                    Cells = new List<string>
                    {
                        "Завершённых талонов (уникальных)",
                        completedCount.ToString(CultureInfo.InvariantCulture),
                        "за период, по строкам List_item"
                    }
                },
                new() { Cells = new List<string> { "Среднее ожидание до вызова, мин", avgWait, "по завершённым строкам" } },
                new() { Cells = new List<string> { "Средняя длительность обслуживания, мин", avgServe, "по завершённым строкам" } }
            }
        };
    }

    public ReportResultViewModel GenerateCabinetLoad(CabinetLoadReportParametersViewModel parameters)
    {
        if (!DateTime.TryParse(
                parameters.WeekStart,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var week))
        {
            week = DateTime.UtcNow.Date;
        }

        var weekStartDo = DateOnly.FromDateTime(week.Date);
        var weekEndDo = weekStartDo.AddDays(7);

        // Нагрузка: число строк List_item за неделю по талонам с date_arrival в интервале.
        var counts = (
            from li in _queue.ListItems.AsNoTracking()
            join a in _queue.Appointments.AsNoTracking() on li.IdAppointment equals a.IdAppointment
            where a.DateArrival >= weekStartDo && a.DateArrival < weekEndDo
            group li by li.IdCabinet into g
            select new { CabinetId = g.Key, Count = g.Count() }
        ).ToList();

        var max = counts.Count == 0 ? 1 : counts.Max(x => x.Count);
        var dict = counts.ToDictionary(x => x.CabinetId, x => x.Count);

        var cabinets = _queue.Cabinets.AsNoTracking().OrderBy(c => c.CabinetNumber).ToList();
        var rows = new List<ReportResultRowViewModel>();
        foreach (var c in cabinets)
        {
            var cnt = dict.GetValueOrDefault(c.IdCabinet, 0);
            var pct = (int)Math.Round(cnt * 100.0 / max);
            rows.Add(new ReportResultRowViewModel
            {
                Cells = new List<string>
                {
                    $"Каб. {c.CabinetNumber}",
                    Math.Clamp(pct, 0, 100).ToString(CultureInfo.InvariantCulture) + "%",
                    parameters.WeekStart
                }
            });
        }

        return new ReportResultViewModel
        {
            GeneratedForReportId = ReportIds.CabinetLoad,
            Title = "Загрузка кабинетов",
            DownloadFileName = "cabinet-load.csv",
            ColumnHeaders = new List<string> { "Кабинет", "Загрузка %", "Неделя с" },
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
