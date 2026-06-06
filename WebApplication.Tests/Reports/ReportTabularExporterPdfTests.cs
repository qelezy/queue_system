using System.Text;
using WebApplication.Services.Reports;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ReportTabularExporterPdfTests
{
    [Fact]
    public void WritePdfBytes_load_and_downtime_detail_before_day_totals_heading_succeeds()
    {
        var result = new ReportResultViewModel
        {
            Title = "Загрузка и простои",
            GeneratedForReportId = ReportIds.LoadAndDowntime,
            ColumnHeaders = ["Дата", "Интервал", "Врач", "Кабинет", "Загрузка", "Простой"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["10-05-2026", "08:00–09:00", "A", "1", "50%", "10%"]),
                ReportResultRowViewModel.FromCells(["", "09:00–10:00", "B", "2", "60%", "5%"]),
                ReportResultRowViewModel.FromCells(
                    ["Итого за день", "", "", "", "", ""],
                    rowClass: "report-load-table__row--day-totals-heading"),
                ReportResultRowViewModel.FromCells(
                    ["", "—", "—", "—", "55%", "8%"],
                    rowClass: "report-load-table__row--day-totals-end")
            ]
        };

        AssertPdf(ReportTabularExporter.WritePdfBytes(result));
    }

    [Fact]
    public void WritePdfBytes_load_and_downtime_day_totals_end_before_period_totals_start_succeeds()
    {
        var result = new ReportResultViewModel
        {
            Title = "Загрузка и простои",
            GeneratedForReportId = ReportIds.LoadAndDowntime,
            ColumnHeaders = ["Дата", "Интервал", "Врач", "Кабинет", "Загрузка", "Простой"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["10-05-2026", "08:00–09:00", "A", "1", "50%", "10%"]),
                ReportResultRowViewModel.FromCells(
                    ["Итого за день", "", "", "", "", ""],
                    rowClass: "report-load-table__row--day-totals-heading"),
                ReportResultRowViewModel.FromCells(
                    ["", "—", "—", "—", "55%", "8%"],
                    rowClass: "report-load-table__row--day-totals-end"),
                ReportResultRowViewModel.FromCells(
                    ["Итого по врачам", "", "", "", "", ""],
                    rowClass: "report-load-table__row--totals-start"),
                ReportResultRowViewModel.FromCells(
                    ["", "—", "A", "—", "55%", "8%"],
                    rowClass: "report-load-table__row--period-total")
            ]
        };

        AssertPdf(ReportTabularExporter.WritePdfBytes(result));
    }

    [Fact]
    public void WritePdfBytes_waiting_before_appointment_detail_before_day_totals_heading_succeeds()
    {
        var result = new ReportResultViewModel
        {
            Title = "Ожидание до приёма",
            GeneratedForReportId = ReportIds.WaitingBeforeAppointment,
            ColumnHeaders = ["Дата", "Интервал", "N", "Среднее", "Медиана", "P90"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["11-05-2026", "08:00–09:00", "3", "5", "4", "9"]),
                ReportResultRowViewModel.FromCells(["", "09:00–10:00", "2", "6", "5", "10"]),
                ReportResultRowViewModel.FromCells(
                    ["Итого за день", "", "", "", "", ""],
                    rowClass: "report-load-table__row--day-totals-heading"),
                ReportResultRowViewModel.FromCells(
                    ["", "—", "5", "5", "4", "10"],
                    rowClass: "report-load-table__row--day-totals-end")
            ]
        };

        AssertPdf(ReportTabularExporter.WritePdfBytes(result));
    }

    [Fact]
    public void WritePdfBytes_waiting_day_totals_end_before_period_totals_start_succeeds()
    {
        var result = new ReportResultViewModel
        {
            Title = "Ожидание до приёма",
            GeneratedForReportId = ReportIds.WaitingBeforeAppointment,
            ColumnHeaders = ["Дата", "Интервал", "N", "Среднее", "Медиана", "P90"],
            Rows =
            [
                ReportResultRowViewModel.FromCells(["11-05-2026", "08:00–09:00", "3", "5", "4", "9"]),
                ReportResultRowViewModel.FromCells(
                    ["Итого за день", "", "", "", "", ""],
                    rowClass: "report-load-table__row--day-totals-heading"),
                ReportResultRowViewModel.FromCells(
                    ["", "—", "3", "5", "4", "9"],
                    rowClass: "report-load-table__row--day-totals-end"),
                ReportResultRowViewModel.FromCells(
                    ["Итого за период", "", "", "", "", ""],
                    rowClass: "report-load-table__row--totals-start"),
                ReportResultRowViewModel.FromCells(
                    ["", "—", "3", "5", "4", "9"],
                    rowClass: "report-load-table__row--period-total")
            ]
        };

        AssertPdf(ReportTabularExporter.WritePdfBytes(result));
    }

    [Fact]
    public void WritePdfBytes_service_route_outcomes_with_dual_charts_succeeds()
    {
        var result = new ReportResultViewModel
        {
            Title = "Исходы обслуживания",
            GeneratedForReportId = ReportIds.ServiceRouteOutcomes,
            ColumnHeaders =
            [
                "Дата",
                "Категория обслуживания",
                "Обращений",
                "Полностью обслужено",
                "С незавершённым обслуживанием"
            ],
            Rows =
            [
                ReportResultRowViewModel.FromCells(
                    ["2026-05-10", "Терапия", "5", "2", "1"])
            ],
            PreviewCharts =
            [
                new ReportPreviewChartDescriptor
                {
                    Kind = "groupedBar",
                    ChartAxisMode = "stacked",
                    Labels = ["10-05-2026"],
                    Datasets =
                    [
                        new ReportPreviewChartDataset { Label = "Полностью обслужено", Values = [2] },
                        new ReportPreviewChartDataset
                        {
                            Label = "С незавершённым обслуживанием",
                            Values = [1]
                        }
                    ],
                    ValueUnit = "шт."
                }
            ]
        };

        AssertPdf(ReportTabularExporter.WritePdfBytes(result));
    }

    [Fact]
    public void WritePdfBytes_service_categories_comparison_horizontal_chart_succeeds()
    {
        var result = new ReportResultViewModel
        {
            Title = "Сравнение категорий обслуживания",
            GeneratedForReportId = ReportIds.ServiceCategoriesComparison,
            PdfOrientation = ReportPdfOrientations.Landscape,
            ColumnHeaders =
            [
                "Категория",
                "Приёмов",
                "Среднее ожидание до вызова",
                "Наименьшее ожидание до вызова",
                "Наибольшее ожидание до вызова",
                "Средняя длительность приёма",
                "Наименьшая длительность приёма",
                "Наибольшая длительность приёма"
            ],
            Rows =
            [
                ReportResultRowViewModel.FromCells(
                    ["ОМС", "2", "4 мин", "3 мин", "5 мин", "9 мин", "8 мин", "10 мин"])
            ],
            PreviewCharts =
            [
                new ReportPreviewChartDescriptor
                {
                    Kind = "horizontalGroupedBar",
                    Labels = ["ОМС", "Платные"],
                    ValueUnit = "мин",
                    Datasets =
                    [
                        new ReportPreviewChartDataset { Label = "Среднее ожидание до вызова", Values = [4, 2] },
                        new ReportPreviewChartDataset { Label = "Средняя длительность приёма", Values = [9, 5] }
                    ]
                }
            ]
        };

        AssertPdf(ReportTabularExporter.WritePdfBytes(result));
    }

    [Fact]
    public void WriteHtmlBytes_service_route_outcomes_emits_date_rowspan()
    {
        var result = CreateServiceRouteOutcomesResultWithGroupedDateRows();
        var html = Encoding.UTF8.GetString(ReportTabularExporter.WriteHtmlBytes(result));
        Assert.Contains("rowspan=\"2\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WritePdfBytes_stages_and_waiting_same_date_rows_succeeds()
    {
        var result = CreateStagesAndWaitingResultWithGroupedDateRows();
        AssertPdf(ReportTabularExporter.WritePdfBytes(result));
    }

    [Fact]
    public void WriteHtmlBytes_stages_and_waiting_emits_date_rowspan()
    {
        var result = CreateStagesAndWaitingResultWithGroupedDateRows();
        var html = Encoding.UTF8.GetString(ReportTabularExporter.WriteHtmlBytes(result));
        Assert.Contains("rowspan=\"2\"", html, StringComparison.Ordinal);
    }

    private static ReportResultViewModel CreateServiceRouteOutcomesResultWithGroupedDateRows() =>
        new()
        {
            Title = "Исходы обслуживания",
            GeneratedForReportId = ReportIds.ServiceRouteOutcomes,
            TableLayout = ReportTableLayouts.DateRowspan,
            DetailRowKind = ReportDetailRowKinds.ArrivedCompleted,
            ColumnHeaders =
            [
                "Дата",
                "Категория обслуживания",
                "Обращений",
                "Полностью обслужено",
                "С незавершённым обслуживанием"
            ],
            Rows =
            [
                ReportResultRowViewModel.FromCells(
                    ["2026-05-10", "Терапия", "5", "2", "1"]),
                ReportResultRowViewModel.FromCells(
                    ["", "Хирургия", "3", "1", "0"])
            ]
        };

    private static ReportResultViewModel CreateStagesAndWaitingResultWithGroupedDateRows() =>
        new()
        {
            Title = "Этапы и ожидание после вызова",
            GeneratedForReportId = ReportIds.StagesAndWaiting,
            TableLayout = ReportTableLayouts.DateRowspan,
            DetailRowKind = ReportDetailRowKinds.StagesAndWaiting,
            ColumnHeaders =
            [
                "Дата",
                "Интервал полного обслуживания",
                "Этапов",
                "Суммарное время обслуживания, мин",
                "Суммарное ожидание после вызова, мин"
            ],
            Rows =
            [
                ReportResultRowViewModel.FromCells(
                    ["2026-05-10", "08:00–09:00", "2", "45.0", "5.0"]),
                ReportResultRowViewModel.FromCells(
                    ["", "09:30–10:30", "3", "50.0", "10.0"])
            ]
        };

    private static void AssertPdf(byte[] pdf)
    {
        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }
}
