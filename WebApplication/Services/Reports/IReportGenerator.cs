using WebApplication.Data;
using WebApplication.Models;

namespace WebApplication.Services.Reports;

/// <summary>
/// Генератор одного отчёта по id. Результат предпросмотра — <see cref="ReportResultViewModel"/> внутри <see cref="ReportGenerateResponse"/>.
/// Поля <see cref="ReportResultViewModel.PreviewCharts"/> / <see cref="ReportResultViewModel.PreviewPieChart"/>
/// нужно заполнять из полных агрегатов источника, а не из усечённой таблицы <see cref="ReportResultViewModel.Rows"/>.
/// </summary>
public interface IReportGenerator
{
    ReportGeneratorKind Kind { get; }

    ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose);
}
