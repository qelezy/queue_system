using System.Globalization;
using System.Linq;
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

        if (string.Equals(reportId, ReportIds.DoctorCabinetLoadDowntime, StringComparison.OrdinalIgnoreCase))
            return new ReportGenerateResponse { Success = true, Implemented = true, Result = GenerateLoadAndDowntimeOffline(request) };

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
            Title = "Сводка по очереди",
            DownloadFileName = "queue-summary.csv",
            ColumnHeaders = ["Показатель", "Значение", "Комментарий"],
            Rows =
            [
                new ReportResultRowViewModel { Cells = ["Период", period, "БД очереди недоступна — примерные значения"] },
                new ReportResultRowViewModel { Cells = ["Кабинет", cab, "фильтр"] },
                new ReportResultRowViewModel { Cells = ["Врач", doc, "фильтр"] },
                new ReportResultRowViewModel
                {
                    Cells = ["Завершённых талонов (уникальных)", "42", "оценка"]
                },
                new ReportResultRowViewModel
                {
                    Cells = ["Среднее ожидание до вызова, мин", "18,5", "оценка"]
                },
                new ReportResultRowViewModel
                {
                    Cells = ["Средняя длительность обслуживания, мин", "22,0", "оценка"]
                }
            ]
        };
    }

    public ReportResultViewModel GenerateLoadAndDowntimeOffline(ReportGenerateRequest request)
    {
        var byCabinet = request.CustomParams is not null && request.CustomParams.TryGetValue("analysisMode", out var am) && string.Equals(am?.Trim(), "cabinet", StringComparison.OrdinalIgnoreCase);

        const int n = 12;
        var headers = new List<string>
        {
            "Дата",
            "Интервал работы",
            "Врач",
            "Специализация врача",
            "Кабинет",
            "Длительность смены, мин",
            "Общая длительность обслуживания, мин",
            "Общая длительность простоя, мин",
            "Средняя длительность простоя, мин",
            "Число интервалов простоя",
            "Загрузка рабочего времени, %",
            "Число завершённых приёмов"
        };

        var rows = new List<ReportResultRowViewModel>();

        if (byCabinet)
        {
            rows.AddRange(
            [
                new()
                {
                    Cells =
                    [
                        "2026-05-01",
                        "07:45–18:30",
                        "Иванов И.И.; Петров П.П.",
                        "Терапия; Хирургия",
                        "Каб. 101",
                        "600",
                        "350",
                        "250",
                        "62,5",
                        "4",
                        "58,3",
                        "7"
                    ]
                },
                new()
                {
                    Cells =
                    [
                        "",
                        "11:00–15:30",
                        "Петров П.П.",
                        "Хирургия",
                        "Каб. 102",
                        "360",
                        "200",
                        "160",
                        "40",
                        "4",
                        "55,6",
                        "4"
                    ]
                },
                new()
                {
                    Cells =
                    [
                        "2026-05-02",
                        "09:00–13:00",
                        "Иванов И.И.",
                        "Терапия",
                        "Каб. 101",
                        "240",
                        "100",
                        "140",
                        "70",
                        "2",
                        "41,7",
                        "2"
                    ]
                }
            ]);
            rows.Add(PadRow(
                n,
                "Итого по кабинетам",
                "",
                "report-load-table__row--totals-start",
                LoadDowntimeTotalsLabelColSpans));
            rows.Add(new()
            {
                Cells =
                [
                    "",
                    "—",
                    "—",
                    "Терапия; Хирургия",
                    "Каб. 101",
                    "840",
                    "450",
                    "390",
                    "65",
                    "6",
                    "53,6",
                    "11"
                ]
            });
            rows.Add(new()
            {
                Cells =
                [
                    "",
                    "—",
                    "—",
                    "Хирургия",
                    "Каб. 102",
                    "360",
                    "200",
                    "160",
                    "40",
                    "4",
                    "55,6",
                    "4"
                ]
            });
        }
        else
        {
            rows.Add(new()
            {
                Cells =
                [
                    "2026-05-01",
                    "08:15–14:00",
                    "Иванов И.И.",
                    "Терапия",
                    "Каб. 101",
                    "480",
                    "250",
                    "230",
                    "57,5",
                    "4",
                    "52,1",
                    "5"
                ]
            });
            rows.Add(new()
            {
                Cells =
                [
                    "",
                    "14:30–19:45",
                    "Иванов И.И.",
                    "Терапия",
                    "Каб. 205",
                    "240",
                    "150",
                    "90",
                    "45",
                    "2",
                    "62,5",
                    "3"
                ]
            });
            rows.Add(new()
            {
                Cells =
                [
                    "",
                    "10:00–16:00",
                    "Петров П.П.",
                    "Хирургия",
                    "Каб. 102",
                    "360",
                    "200",
                    "160",
                    "40",
                    "4",
                    "55,6",
                    "4"
                ]
            });
            rows.Add(PadRow(
                n,
                "Итого за день",
                "",
                "report-load-table__row--day-totals-heading",
                LoadDowntimeTotalsLabelColSpans));
            rows.Add(new()
            {
                Cells =
                [
                    "",
                    "—",
                    "Иванов И.И.",
                    "Терапия",
                    "—",
                    "720",
                    "400",
                    "320",
                    "53,3",
                    "6",
                    "55,6",
                    "8"
                ]
            });
            rows.Add(new()
            {
                RowClass = "report-load-table__row--day-totals-end",
                Cells =
                [
                    "",
                    "—",
                    "Петров П.П.",
                    "Хирургия",
                    "—",
                    "360",
                    "200",
                    "160",
                    "40",
                    "4",
                    "55,6",
                    "4"
                ]
            });
            rows.Add(new()
            {
                Cells =
                [
                    "2026-05-02",
                    "08:30–12:30",
                    "Иванов И.И.",
                    "Терапия",
                    "Каб. 101",
                    "240",
                    "100",
                    "140",
                    "70",
                    "2",
                    "41,7",
                    "2"
                ]
            });
            rows.Add(PadRow(
                n,
                "Итого за день",
                "",
                "report-load-table__row--day-totals-heading",
                LoadDowntimeTotalsLabelColSpans));
            rows.Add(new()
            {
                RowClass = "report-load-table__row--day-totals-end",
                Cells =
                [
                    "",
                    "—",
                    "Иванов И.И.",
                    "Терапия",
                    "—",
                    "240",
                    "100",
                    "140",
                    "70",
                    "2",
                    "41,7",
                    "2"
                ]
            });
            rows.Add(PadRow(
                n,
                "Итого по врачам",
                "",
                "report-load-table__row--totals-start",
                LoadDowntimeTotalsLabelColSpans));
            rows.Add(new()
            {
                Cells =
                [
                    "",
                    "—",
                    "Иванов И.И.",
                    "Терапия",
                    "—",
                    "960",
                    "500",
                    "460",
                    "57,5",
                    "8",
                    "52,1",
                    "9"
                ]
            });
            rows.Add(new()
            {
                Cells =
                [
                    "",
                    "—",
                    "Петров П.П.",
                    "Хирургия",
                    "—",
                    "360",
                    "200",
                    "160",
                    "40",
                    "4",
                    "55,6",
                    "4"
                ]
            });
        }

        return new ReportResultViewModel
        {
            GeneratedForReportId = ReportIds.DoctorCabinetLoadDowntime,
            Title = "Загрузка и простои",
            DownloadFileName = "load-and-downtime.csv",
            ColumnHeaders = headers,
            Rows = rows,
            PreviewPieChart = MockLoadDowntimePreviewPie(byCabinet)
        };
    }

    private static ReportPreviewPieChart MockLoadDowntimePreviewPie(bool byCabinet) =>
        byCabinet
            ? new ReportPreviewPieChart
            {
                Labels = ["Обслуживание (мин)", "Простой (мин)"],
                Values = [650, 550]
            }
            : new ReportPreviewPieChart
            {
                Labels = ["Обслуживание (мин)", "Простой (мин)"],
                Values = [700, 620]
            };

    private static readonly List<int> LoadDowntimeTotalsLabelColSpans =
        [2, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1];

    private static ReportResultRowViewModel PadRow(
        int colCount,
        string c0,
        string c1,
        string? rowClass = null,
        IReadOnlyList<int>? cellColSpans = null)
    {
        var cells = new List<string> { c0, c1 };
        while (cells.Count < colCount)
            cells.Add("");
        return new ReportResultRowViewModel
        {
            Cells = cells,
            RowClass = rowClass,
            CellColSpans = cellColSpans is null ? null : cellColSpans.ToList()
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
                parameters.WeekStart
            ]
        }).ToList();

        return new ReportResultViewModel
        {
            GeneratedForReportId = ReportIds.CabinetLoad,
            Title = "Загрузка кабинетов",
            DownloadFileName = "cabinet-load.csv",
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

        if (string.Equals(reportId, ReportIds.DoctorCabinetLoadDowntime, StringComparison.OrdinalIgnoreCase))
        {
            var p = new ReportGenerateRequest
            {
                ReportId = ReportIds.DoctorCabinetLoadDowntime,
                DateFrom = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTo = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CustomParams = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["analysisMode"] = "doctor" }
            };
            return ToCsvBytes(GenerateLoadAndDowntimeOffline(p));
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
