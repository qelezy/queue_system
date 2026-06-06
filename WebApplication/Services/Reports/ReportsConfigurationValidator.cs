namespace WebApplication.Services.Reports;

public static class ReportsConfigurationValidator
{
    private static readonly HashSet<string> AllowedTableLayouts = new(StringComparer.OrdinalIgnoreCase)
    {
        ReportTableLayouts.Standard,
        ReportTableLayouts.DateRowspan
    };

    private static readonly HashSet<string> AllowedPdfOrientations = new(StringComparer.OrdinalIgnoreCase)
    {
        ReportPdfOrientations.Landscape,
        ReportPdfOrientations.Portrait
    };

    private static readonly HashSet<string> AllowedDetailRowKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ReportDetailRowKinds.Standard,
        ReportDetailRowKinds.LoadDowntime,
        ReportDetailRowKinds.StagesAndWaiting,
        ReportDetailRowKinds.ArrivedCompleted,
        ReportDetailRowKinds.AppointmentDuration,
        ReportDetailRowKinds.WaitingBeforeAppointment
    };

    public static void Validate(IReportsCatalog catalog, IReadOnlyCollection<IReportGenerator> generators)
    {
        var catalogItems = catalog.GetCatalog();
        var catalogIds = catalogItems.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var generatorByKind = generators.GroupBy(g => g.Kind).ToDictionary(g => g.Key, g => g.ToList());

        var seenKinds = new HashSet<ReportGeneratorKind>();
        foreach (var item in catalogItems)
        {
            if (!seenKinds.Add(item.GeneratorKind))
                throw new InvalidOperationException(
                    $"Дублирующийся GeneratorKind «{item.GeneratorKind}» в Reports:Catalog (appsettings.json).");

            if (!AllowedTableLayouts.Contains(item.TableLayout))
                throw new InvalidOperationException(
                    $"Недопустимый TableLayout «{item.TableLayout}» для отчёта «{item.Id}».");

            if (!AllowedPdfOrientations.Contains(item.PdfOrientation))
                throw new InvalidOperationException(
                    $"Недопустимый PdfOrientation «{item.PdfOrientation}» для отчёта «{item.Id}».");

            if (!AllowedDetailRowKinds.Contains(item.DetailRowKind))
                throw new InvalidOperationException(
                    $"Недопустимый DetailRowKind «{item.DetailRowKind}» для отчёта «{item.Id}».");

            if (!generatorByKind.TryGetValue(item.GeneratorKind, out var gens) || gens.Count != 1)
                throw new InvalidOperationException(
                    $"Для GeneratorKind «{item.GeneratorKind}» (отчёт «{item.Id}») требуется ровно один IReportGenerator.");
        }

        foreach (var (kind, gens) in generatorByKind)
        {
            if (!catalogItems.Any(x => x.GeneratorKind == kind))
                throw new InvalidOperationException(
                    $"Зарегистрирован генератор «{kind}», но в Reports:Catalog нет соответствующей записи.");

            if (gens.Count > 1)
                throw new InvalidOperationException(
                    $"Несколько генераторов с Kind «{kind}».");
        }

    }
}
