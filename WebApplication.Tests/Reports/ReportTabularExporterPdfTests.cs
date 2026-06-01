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
                "Приёмов",
                "С завершённым маршрутом",
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
                    Kind = "doughnut",
                    Labels = ["С завершённым маршрутом", "С незавершённым обслуживанием"],
                    Values = [2, 1]
                },
                new ReportPreviewChartDescriptor
                {
                    Kind = "groupedBar",
                    Labels = ["10-05-2026"],
                    Datasets =
                    [
                        new ReportPreviewChartDataset { Label = "С завершённым маршрутом", Values = [2] },
                        new ReportPreviewChartDataset
                        {
                            Label = "С незавершённым обслуживанием",
                            Values = [1]
                        }
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
    public void WritePdfBytes_route_and_pauses_same_date_rows_succeeds()
    {
        var result = CreateRouteAndPausesResultWithGroupedDateRows();
        AssertPdf(ReportTabularExporter.WritePdfBytes(result));
    }

    [Fact]
    public void WriteHtmlBytes_route_and_pauses_emits_date_rowspan()
    {
        var result = CreateRouteAndPausesResultWithGroupedDateRows();
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
                "Приёмов",
                "С завершённым маршрутом",
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

    private static ReportResultViewModel CreateRouteAndPausesResultWithGroupedDateRows() =>
        new()
        {
            Title = "Этапы и паузы",
            GeneratedForReportId = ReportIds.RouteAndPauses,
            TableLayout = ReportTableLayouts.DateRowspan,
            DetailRowKind = ReportDetailRowKinds.RouteAndPauses,
            ColumnHeaders =
            [
                "Дата",
                "Интервал полного обслуживания",
                "Этапов",
                "Суммарное время обслуживания, мин",
                "Сумма пауз до начала приёма, мин"
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
