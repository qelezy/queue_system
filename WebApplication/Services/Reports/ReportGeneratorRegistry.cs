using System.Diagnostics.CodeAnalysis;
using WebApplication.Data;
using WebApplication.Models;

namespace WebApplication.Services.Reports;

public sealed class ReportGeneratorRegistry
{
    private readonly Dictionary<string, IReportGenerator> _generators;

    public ReportGeneratorRegistry(IEnumerable<IReportGenerator> generators)
    {
        _generators = new Dictionary<string, IReportGenerator>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in generators)
        {
            if (string.IsNullOrWhiteSpace(g.ReportId))
                continue;
            _generators[g.ReportId.Trim()] = g;
        }
    }

    public bool TryGenerate(
        string reportId,
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        [NotNullWhen(true)] out ReportGenerateResponse? response)
    {
        if (string.IsNullOrWhiteSpace(reportId) || !_generators.TryGetValue(reportId.Trim(), out var gen))
        {
            response = null;
            return false;
        }

        response = gen.Generate(request, queue);
        return true;
    }
}
