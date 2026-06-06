using System.Globalization;
using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports;

namespace WebApplication.Services.Reports.Catalog;

internal static class AppointmentDurationReportBuilder
{
    internal const string ModeDoctor = "doctor";
    internal const string ModeSpecialty = "specialty";
    internal const string ModeCabinet = "cabinet";

    private static readonly string[] TailMetricHeaders =
    [
        "Завершённых приёмов",
        "Средняя длительность",
        "Норматив",
        "Работает быстрее на",
        "Работает медленнее на",
        "Самый короткий приём",
        "Самый длинный приём"
    ];

    internal static string ParseAnalysisMode(IReadOnlyDictionary<string, string?>? customParams)
    {
        if (customParams is not null
            && customParams.TryGetValue("analysisMode", out var raw))
        {
            var mode = raw?.Trim();
            if (string.Equals(mode, ModeSpecialty, StringComparison.OrdinalIgnoreCase))
                return ModeSpecialty;
            if (string.Equals(mode, ModeCabinet, StringComparison.OrdinalIgnoreCase))
                return ModeCabinet;
        }

        return ModeDoctor;
    }

    internal static string FormatCabinetLabel(string? cabinetNumber) =>
        CatalogReportAnalysisHelper.FormatCabinetLabel(cabinetNumber);

    internal static string NormalizeDimensionLabel(string? label) =>
        string.IsNullOrWhiteSpace(label) ? "—" : label.Trim();

    internal static ReportResultViewModel BuildReport(
        IReadOnlyList<DurationObservation> observations,
        DateOnly fromDo,
        DateOnly toDo,
        string analysisMode,
        ReportGenerationPurpose purpose)
    {
        var mode = ResolveMode(analysisMode);
        var model = Build(observations, fromDo, toDo, mode);
        ApplyPeriodTotalsAndPreview(model, observations, mode, purpose);
        return model;
    }

    private static string ResolveMode(string analysisMode) =>
        analysisMode switch
        {
            ModeSpecialty => ModeSpecialty,
            ModeCabinet => ModeCabinet,
            _ => ModeDoctor
        };

    private static ReportResultViewModel Build(
        IReadOnlyList<DurationObservation> observations,
        DateOnly fromDo,
        DateOnly toDo,
        string mode)
    {
        var includeSpecialtyCol = mode == ModeDoctor;
        var dimensionHeader = mode switch
        {
            ModeSpecialty => "Специальность",
            ModeCabinet => "Кабинет",
            _ => "Врач"
        };

        var headers = includeSpecialtyCol
            ? new List<string>(["Дата", dimensionHeader, "Специализация врача", ..TailMetricHeaders])
            : new List<string>(["Дата", dimensionHeader, ..TailMetricHeaders]);

        var rows = new List<ReportResultRowViewModel>();

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            var dayObs = observations.Where(o => o.Date == day).ToList();
            if (dayObs.Count == 0)
                continue;

            var isFirstDetail = true;

            foreach (var g in dayObs
                         .GroupBy(o => o.DimensionLabel)
                         .OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var groupItems = g.ToList();
                var metrics = ComputeMetrics(groupItems);
                var specialtyCell = includeSpecialtyCol
                    ? FormatSpecialtyList(groupItems.Select(o => o.SpecialtyDefinition))
                    : null;

                rows.Add(BuildDetailRow(
                    isFirstDetail ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                    g.Key,
                    specialtyCell,
                    metrics,
                    includeSpecialtyCol));
                isFirstDetail = false;
            }
        }

        var previewCharts = BuildPeriodPreviewCharts(observations);

        return new ReportResultViewModel
        {
            ColumnHeaders = headers,
            Rows = rows,
            PreviewCharts = previewCharts
        };
    }

    private static List<ReportPreviewChartDescriptor>? BuildPeriodPreviewCharts(
        IReadOnlyList<DurationObservation> observations)
    {
        var categoryLabels = new List<string>();
        var avgMinutes = new List<double?>();
        var normMinutes = new List<double?>();
        var deviationMinutes = new List<double?>();

        var slices = observations
            .GroupBy(o => o.DimensionLabel)
            .Select(g => (Label: g.Key, Minutes: ComputePeriodChartMinutes(g.ToList())))
            .OrderByDescending(s => s.Minutes.Average ?? 0)
            .ThenBy(s => s.Label, StringComparer.Ordinal)
            .ToList();

        foreach (var slice in slices)
        {
            categoryLabels.Add(slice.Label);
            avgMinutes.Add(slice.Minutes.Average);
            normMinutes.Add(slice.Minutes.Normative);
            deviationMinutes.Add(slice.Minutes.Deviation);
        }

        return ReportPreviewChartDescriptors.ForAppointmentDurationPeriodHorizontalGroupedBar(
            categoryLabels,
            avgMinutes,
            normMinutes,
            deviationMinutes);
    }

    private static (double? Average, double? Normative, double? Deviation) ComputePeriodChartMinutes(
        IReadOnlyList<DurationObservation> items)
    {
        var slice = ComputePeriodSliceMinutes(items);
        return (
            CatalogReportShared.RoundDurationDisplayChartValue(slice.AverageMinutes),
            CatalogReportShared.RoundDurationDisplayChartValue(slice.NormativeMinutes),
            slice.DeviationMinutes == 0
                ? null
                : CatalogReportShared.RoundDurationDisplayChartValue(slice.DeviationMinutes));
    }

    private static (double AverageMinutes, double NormativeMinutes, double DeviationMinutes) ComputePeriodSliceMinutes(
        IReadOnlyList<DurationObservation> items)
    {
        var svc = items.Select(i => i.SvcMin).ToList();
        var norms = items.Select(i => (double)i.NormMinutes).ToList();
        var avg = CatalogReportShared.AverageDurationMinutes(svc);
        var normAvg = norms.Average();
        return (avg, normAvg, avg - normAvg);
    }

    private static ReportResultRowViewModel BuildDetailRow(
        string dateCell,
        string dimension,
        string? specialtyCell,
        DurationMetrics metrics,
        bool includeSpecialtyCol)
    {
        if (includeSpecialtyCol)
        {
            return ReportCsvCells.FromDisplayCells(
            [
                dateCell,
                dimension,
                specialtyCell ?? "—",
                metrics.Count,
                metrics.Average,
                metrics.Normative,
                metrics.WorksFaster,
                metrics.WorksSlower,
                metrics.Min,
                metrics.Max
            ],
            new Dictionary<int, double?>
            {
                [4] = metrics.AverageExact,
                [5] = metrics.NormativeExact,
                [6] = metrics.WorksFasterExact,
                [7] = metrics.WorksSlowerExact,
                [8] = metrics.MinExact,
                [9] = metrics.MaxExact
            });
        }

        return ReportCsvCells.FromDisplayCells(
        [
            dateCell,
            dimension,
            metrics.Count,
            metrics.Average,
            metrics.Normative,
            metrics.WorksFaster,
            metrics.WorksSlower,
            metrics.Min,
            metrics.Max
        ],
        new Dictionary<int, double?>
        {
            [3] = metrics.AverageExact,
            [4] = metrics.NormativeExact,
            [5] = metrics.WorksFasterExact,
            [6] = metrics.WorksSlowerExact,
            [7] = metrics.MinExact,
            [8] = metrics.MaxExact
        });
    }

    private static readonly int[] PeriodTotalsLabelColSpansDoctor = [3, 0, 0, 1, 1, 1, 1, 1, 1, 1];
    private static readonly int[] PeriodTotalsLabelColSpansSlice = [2, 0, 1, 1, 1, 1, 1, 1, 1];

    private static void ApplyPeriodTotalsAndPreview(
        ReportResultViewModel model,
        IReadOnlyList<DurationObservation> observations,
        string mode,
        ReportGenerationPurpose purpose)
    {
        var includeSpecialtyCol = mode == ModeDoctor;
        var periodHeading = CatalogReportPreviewHelper.PeriodTotalsLabel;
        var detailRows = model.Rows;

        if (CatalogReportPreviewHelper.HasNoDetailRows(detailRows))
        {
            model.PreviewCharts = null;
            return;
        }

        if (purpose != ReportGenerationPurpose.JsonPreview)
        {
            AppendPeriodTotals(detailRows, observations, periodHeading, includeSpecialtyCol);
            return;
        }

        var sliceCount = observations.Select(o => o.DimensionLabel).Distinct(StringComparer.Ordinal).Count();
        var previewTailReserved = 1 + Math.Max(sliceCount, 1);

        if (detailRows.Count > ReportPreviewLimits.MaxTableRows)
        {
            var maxDetail = Math.Max(0, ReportPreviewLimits.MaxTableRows - previewTailReserved);
            model.PreviewRowsTotal = detailRows.Count;
            model.PreviewRowLimit = ReportPreviewLimits.MaxTableRows;
            model.Rows =
            [
                ..detailRows.Take(maxDetail),
                ..BuildPeriodTotalsRows(observations, periodHeading, includeSpecialtyCol)
            ];
            return;
        }

        AppendPeriodTotals(detailRows, observations, periodHeading, includeSpecialtyCol);
    }

    private static void AppendPeriodTotals(
        List<ReportResultRowViewModel> rows,
        IReadOnlyList<DurationObservation> observations,
        string heading,
        bool includeSpecialtyCol)
    {
        foreach (var r in BuildPeriodTotalsRows(observations, heading, includeSpecialtyCol))
            rows.Add(r);
    }

    private static IEnumerable<ReportResultRowViewModel> BuildPeriodTotalsRows(
        IReadOnlyList<DurationObservation> observations,
        string heading,
        bool includeSpecialtyCol)
    {
        if (includeSpecialtyCol)
        {
            yield return ReportResultRowViewModel.FromCells(
                [heading, "", "", "", "", "", "", "", "", ""],
                rowClass: "report-load-table__row--totals-start",
                cellColSpans: PeriodTotalsLabelColSpansDoctor);
        }
        else
        {
            yield return ReportResultRowViewModel.FromCells(
                [heading, "", "", "", "", "", "", "", ""],
                rowClass: "report-load-table__row--totals-start",
                cellColSpans: PeriodTotalsLabelColSpansSlice);
        }

        foreach (var g in observations
                     .GroupBy(o => o.DimensionLabel)
                     .OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var items = g.ToList();
            var metrics = ComputeMetrics(items);
            var specialtyCell = includeSpecialtyCol
                ? FormatSpecialtyList(items.Select(o => o.SpecialtyDefinition))
                : null;

            if (includeSpecialtyCol)
            {
                yield return ReportResultRowViewModel.FromCells(
                    ["", g.Key, specialtyCell ?? "—", metrics.Count, metrics.Average, metrics.Normative, metrics.WorksFaster, metrics.WorksSlower, metrics.Min, metrics.Max],
                    rowClass: "report-load-table__row--period-total");
            }
            else
            {
                yield return ReportResultRowViewModel.FromCells(
                    ["", g.Key, metrics.Count, metrics.Average, metrics.Normative, metrics.WorksFaster, metrics.WorksSlower, metrics.Min, metrics.Max],
                    rowClass: "report-load-table__row--period-total");
            }
        }
    }

    private static string FormatSpecialtyList(IEnumerable<string?> definitions)
    {
        var parts = definitions
            .Select(d => d?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        return parts.Count == 0 ? "—" : string.Join("; ", parts);
    }

    private static DurationMetrics ComputeMetrics(IReadOnlyList<DurationObservation> items)
    {
        if (items.Count == 0)
            return new DurationMetrics("0", "—", "—", "—", "—", "—", "—", 0, 0, null, null, 0, 0);

        var svc = items.Select(i => i.SvcMin).ToList();
        var svcExact = items.Select(i => i.SvcMinExact).ToList();
        var slice = ComputePeriodSliceMinutes(items);
        var avg = slice.AverageMinutes;
        var normAvg = slice.NormativeMinutes;
        var deviation = slice.DeviationMinutes;
        var avgExact = CatalogReportShared.AverageDurationMinutesExact(svcExact);
        var normExact = items.Select(i => (double)i.NormMinutes).Average();
        var deviationExact = avgExact - normExact;
        var worksFaster = deviation < 0
            ? CatalogReportShared.FormatDuration(Math.Abs(deviation))
            : "—";
        var worksSlower = deviation > 0
            ? CatalogReportShared.FormatDuration(deviation)
            : "—";
        var appointmentCount = items.Select(i => i.IdAppointment).Distinct().Count();
        return new DurationMetrics(
            appointmentCount.ToString(CultureInfo.InvariantCulture),
            CatalogReportShared.FormatDuration(avg),
            CatalogReportShared.FormatDuration(normAvg),
            worksFaster,
            worksSlower,
            CatalogReportShared.FormatDuration(CatalogReportShared.MinDurationMinutes(svc)),
            CatalogReportShared.FormatDuration(CatalogReportShared.MaxDurationMinutes(svc)),
            avgExact,
            normExact,
            deviationExact < 0 ? Math.Abs(deviationExact) : null,
            deviationExact > 0 ? deviationExact : null,
            CatalogReportShared.MinDurationMinutesExact(svcExact),
            CatalogReportShared.MaxDurationMinutesExact(svcExact));
    }

    private readonly record struct DurationMetrics(
        string Count,
        string Average,
        string Normative,
        string WorksFaster,
        string WorksSlower,
        string Min,
        string Max,
        double AverageExact,
        double NormativeExact,
        double? WorksFasterExact,
        double? WorksSlowerExact,
        double MinExact,
        double MaxExact);

    internal readonly record struct DurationObservation(
        DateOnly Date,
        string DimensionLabel,
        int IdAppointment,
        double SvcMin,
        double SvcMinExact,
        int NormMinutes,
        string? SpecialtyDefinition);
}
