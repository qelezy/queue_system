using System.Globalization;
using WebApplication.Models;

namespace WebApplication.Services.Reports.Catalog;

internal static class BottleneckRankingReportBuilder
{
    internal const string ModeDoctor = "doctor";
    internal const string ModeCabinet = "cabinet";

    internal const int TopN = 15;

    internal static string ParseAnalysisMode(IReadOnlyDictionary<string, string?>? customParams)
    {
        if (customParams is not null
            && customParams.TryGetValue("analysisMode", out var raw)
            && string.Equals(raw?.Trim(), ModeCabinet, StringComparison.OrdinalIgnoreCase))
            return ModeCabinet;

        return ModeDoctor;
    }

    internal static string FormatCabinetLabel(string? cabinetNumber) =>
        string.IsNullOrWhiteSpace(cabinetNumber)
            ? "—"
            : "Каб. " + cabinetNumber.Trim();

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<BottleneckRankingQueries.EntityMetrics> metrics,
        string analysisMode,
        ReportGenerationPurpose purpose)
    {
        var byCabinet = string.Equals(analysisMode, ModeCabinet, StringComparison.OrdinalIgnoreCase);

        var ranked = metrics
            .OrderByDescending(m => m.TotalDelayMin)
            .ThenBy(m => m.EntityName, StringComparer.OrdinalIgnoreCase)
            .Take(TopN)
            .ToList();

        var table = ranked
            .Select(m => ReportResultRowViewModel.FromCells(
            [
                m.EntityName,
                m.SpecialtyLabels,
                m.QueueIncidents.ToString(CultureInfo.InvariantCulture),
                FormatDelayMinutes(m.TotalDelayMin),
                FormatAvgDelay(m.AvgDelayMin),
                FormatDelayMinutes(m.MinDelayMin),
                FormatDelayMinutes(m.MaxDelayMin),
                m.OverNormCount.ToString(CultureInfo.InvariantCulture)
            ]))
            .ToList();

        var model = new ReportResultViewModel
        {
            ColumnHeaders = byCabinet
                ?
                [
                    "Кабинет",
                    "Специализация врача",
                    "Инцидентов задержки",
                    "Сумма задержек, минут",
                    "Средняя задержка, минут",
                    "Минимальная задержка, минут",
                    "Максимальная задержка, минут",
                    "Превышений норматива"
                ]
                :
                [
                    "Врач",
                    "Специализация",
                    "Инцидентов задержки",
                    "Сумма задержек, минут",
                    "Средняя задержка, минут",
                    "Минимальная задержка, минут",
                    "Максимальная задержка, минут",
                    "Превышений норматива"
                ],
            Rows = table
        };

        CatalogReportShared.ApplyPreviewRowCap(model, purpose);
        return model;
    }

    private static string FormatDelayMinutes(double minutes) =>
        Math.Round(minutes, 1).ToString(CultureInfo.InvariantCulture);

    private static string FormatAvgDelay(double? minutes) =>
        minutes is null ? "—" : CatalogReportShared.F1(minutes.Value);
}
