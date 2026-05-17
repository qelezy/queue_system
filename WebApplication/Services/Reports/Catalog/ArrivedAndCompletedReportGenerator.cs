using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Models;

namespace WebApplication.Services.Reports.Catalog;

/// <summary>
/// Предпросмотр: <see cref="ReportResultViewModel.PreviewCharts"/> от полных сумм колонок 4–6 за период;
/// таблица при <see cref="ReportGenerationPurpose.JsonPreview"/> может усечься (см. <see cref="ReportPreviewLimits"/>).
/// </summary>
public sealed class ArrivedAndCompletedReportGenerator : IReportGenerator
{
    public ReportGeneratorKind Kind => ReportGeneratorKind.ArrivedAndCompleted;

    public ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);

        var categories = queue.Categories.AsNoTracking()
            .ToDictionary(c => c.IdCategory, c => (c.Name ?? "—", c.Priority));

        var appointments = queue.Appointments.AsNoTracking()
            .Where(a => a.DateArrival >= fromDo && a.DateArrival <= toDo)
            .Select(a => new ArrivedAndCompletedReportBuilder.ArrivedAppointmentObservation(
                a.IdAppointment,
                a.DateArrival,
                a.IdCategory))
            .ToList();

        var appIds = appointments.Select(a => a.IdAppointment).ToHashSet();
        var listItems = queue.ListItems.AsNoTracking()
            .Where(li => appIds.Contains(li.IdAppointment))
            .Select(li => new ArrivedAndCompletedReportBuilder.ArrivedListItemObservation(
                li.IdAppointment,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing))
            .ToList();

        var model = ArrivedAndCompletedReportBuilder.BuildReport(
            appointments, listItems, categories, purpose);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = model };
    }
}
