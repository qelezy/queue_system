using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Services.Reports.Catalog;

namespace WebApplication.Services.Reports;

public sealed class ReportGenerationService : IReportGenerationService
{
    private readonly ElectronicQueueDbContext _queue;
    private readonly ReportGeneratorRegistry _reportGenerators;
    private readonly IReportsCatalog _catalog;

    public ReportGenerationService(
        ElectronicQueueDbContext queue,
        ReportGeneratorRegistry reportGenerators,
        IReportsCatalog catalog)
    {
        _queue = queue;
        _reportGenerators = reportGenerators;
        _catalog = catalog;
    }

    public IReadOnlyList<ReportSelectOption> GetCabinetOptions() =>
        _queue.Cabinets
            .AsNoTracking()
            .OrderBy(c => c.CabinetNumber)
            .Select(c => new ReportSelectOption { Id = c.IdCabinet, Label = c.CabinetNumber ?? "—" })
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

    public ReportGenerateResponse Generate(ReportGenerateRequest request, ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        var reportId = request.ReportId?.Trim() ?? "";
        if (_reportGenerators.TryGenerate(reportId, request, _queue, purpose, out var fromRegistry) && fromRegistry is not null)
            return fromRegistry;

        return new ReportGenerateResponse
        {
            Success = true,
            Implemented = false,
            Message = "Формирование выбранного отчёта пока не реализовано."
        };
    }

    public (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request)
    {
        var generated = Generate(request, ReportGenerationPurpose.ExportOrFull);
        if (!generated.Implemented || generated.Result is null)
        {
            var stub = new ReportResultViewModel
            {
                GeneratedForReportId = "report",
                DownloadFileName = "report-not-implemented.csv",
                ColumnHeaders = ["report", "status"],
                Rows = [new ReportResultRowViewModel { Cells = ["not_implemented", "true"] }]
            };
            return ReportTabularExporter.Export(stub, "csv", request, ResolveGeneratorKind(request.ReportId));
        }

        return ReportTabularExporter.Export(
            generated.Result,
            request.Format,
            request,
            ResolveGeneratorKind(request.ReportId));
    }

    private ReportGeneratorKind? ResolveGeneratorKind(string? reportId)
    {
        var rid = reportId?.Trim() ?? "";
        return _catalog.TryGetItem(rid, out var item) && item is not null ? item.GeneratorKind : null;
    }

    public byte[] BuildDemoCsv(string reportId, string? analysisMode = null)
    {
        var rid = reportId.Trim();
        if (!_catalog.TryGetItem(rid, out var item) || item is null)
            return Encoding.UTF8.GetBytes("reportId;status\nunknown;not_found\n");

        var p = new ReportGenerateRequest
        {
            ReportId = rid,
            DateFrom = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTo = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            CustomParams = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        if (item.GeneratorKind is ReportGeneratorKind.LoadAndDowntime or ReportGeneratorKind.ServiceDelays)
        {
            p.CustomParams["analysisMode"] = string.Equals(analysisMode?.Trim(), "cabinet", StringComparison.OrdinalIgnoreCase)
                ? "cabinet"
                : "doctor";
        }
        else if (item.GeneratorKind == ReportGeneratorKind.AppointmentDuration)
        {
            p.CustomParams["analysisMode"] = AppointmentDurationReportBuilder.ParseAnalysisMode(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analysisMode"] = string.IsNullOrWhiteSpace(analysisMode) ? "doctor" : analysisMode.Trim()
                });
        }

        if (_reportGenerators.TryGenerate(rid, p, _queue, ReportGenerationPurpose.ExportOrFull, out var resp)
            && resp?.Result is not null)
            return ReportTabularExporter.WriteCsvBytes(resp.Result);

        return Encoding.UTF8.GetBytes("reportId;status\nunknown;not_found\n");
    }
}
