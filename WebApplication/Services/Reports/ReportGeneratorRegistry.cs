using System.Diagnostics.CodeAnalysis;
using WebApplication.Data;

namespace WebApplication.Services.Reports;

public sealed class ReportGeneratorRegistry
{
    private readonly Dictionary<string, IReportGenerator> _generators;
    private readonly ReportCatalogMetadataEnricher _metadataEnricher;

    public ReportGeneratorRegistry(
        IEnumerable<IReportGenerator> generators,
        IReportsCatalog catalog,
        ReportCatalogMetadataEnricher metadataEnricher)
    {
        _metadataEnricher = metadataEnricher;
        _generators = new Dictionary<string, IReportGenerator>(StringComparer.OrdinalIgnoreCase);

        var byKind = generators
            .GroupBy(g => g.Kind)
            .ToDictionary(g => g.Key, g => g.Single());

        foreach (var item in catalog.GetCatalog())
        {
            if (!byKind.TryGetValue(item.GeneratorKind, out var gen))
                continue;

            _generators[item.Id] = gen;
        }
    }

    public IReadOnlyCollection<string> RegisteredReportIds => _generators.Keys;

    public bool TryGenerate(
        string reportId,
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose,
        [NotNullWhen(true)] out ReportGenerateResponse? response)
    {
        if (string.IsNullOrWhiteSpace(reportId) || !_generators.TryGetValue(reportId.Trim(), out var gen))
        {
            response = null;
            return false;
        }

        response = gen.Generate(request, queue, purpose);
        if (response.Result is not null)
            _metadataEnricher.ApplyToResult(response.Result, reportId.Trim());

        return true;
    }
}
