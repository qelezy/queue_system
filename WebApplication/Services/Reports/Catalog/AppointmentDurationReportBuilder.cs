using System.Globalization;
using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports.Charts;

namespace WebApplication.Services.Reports.Catalog;

/// <summary>Сборка таблицы и диаграммы отчёта «Длительность приёма» (дата × срез).</summary>
internal static class AppointmentDurationReportBuilder
{
    internal const string ModeDoctor = "doctor";
    internal const string ModeSpecialty = "specialty";
    internal const string ModeCabinet = "cabinet";

    internal const int ChartTopSeriesCount = 8;

    private static readonly string[] TailMetricHeaders =
    [
        "Завершённых приёмов",
        "Средняя длительность, мин",
        "Норматив, мин",
        "Отклонение, мин",
        "Минимум, мин",
        "Максимум, мин"
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
        var chartDays = new List<DateOnly>();
        var topSeries = SelectTopSeries(observations, ChartTopSeriesCount);
        var seriesDatasets = topSeries
            .Select(s => new ReportPreviewChartDataset { Label = s, Values = new List<double>(), NormValues = new List<double>() })
            .ToList();
        var seriesIndex = topSeries.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            var dayObs = observations.Where(o => o.Date == day).ToList();
            if (dayObs.Count == 0)
                continue;

            chartDays.Add(day);
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

            foreach (var series in topSeries)
            {
                if (!seriesIndex.TryGetValue(series, out var idx))
                    continue;
                var sliceObs = dayObs.Where(o => o.DimensionLabel == series).ToList();
                if (sliceObs.Count == 0)
                {
                    seriesDatasets[idx].Values.Add(ChartDatasetValues.Missing);
                    seriesDatasets[idx].NormValues!.Add(ChartDatasetValues.Missing);
                    continue;
                }

                var svcValues = sliceObs.Select(o => o.SvcMin).ToList();
                seriesDatasets[idx].Values.Add(CatalogReportShared.RoundMetric(svcValues.Average()));
                var normValues = sliceObs.Select(o => (double)o.NormMinutes).ToList();
                seriesDatasets[idx].NormValues!.Add(CatalogReportShared.RoundMetric(normValues.Average()));
            }
        }

        var chartDatasets = seriesDatasets;
        var axis = GroupedBarChartTimeAxis.Prepare(chartDays, chartDatasets, GroupedBarBucketAggregation.Average);
        var previewCharts = ReportPreviewChartDescriptors.ForAppointmentDurationDailyGroupedBar(
            axis.Labels.ToList(),
            axis.Datasets.ToList());
        GroupedBarChartTimeAxis.SetGroupedBarFootnote(previewCharts, axis.Footnote);

        return new ReportResultViewModel
        {
            ColumnHeaders = headers,
            Rows = rows,
            PreviewCharts = previewCharts
        };
    }

    private static List<string> SelectTopSeries(IReadOnlyList<DurationObservation> observations, int topN) =>
        observations
            .GroupBy(o => o.DimensionLabel)
            .OrderByDescending(g => g.Count())
            .Take(topN)
            .Select(g => g.Key)
            .ToList();

    private static ReportResultRowViewModel BuildDetailRow(
        string dateCell,
        string dimension,
        string? specialtyCell,
        DurationMetrics metrics,
        bool includeSpecialtyCol)
    {
        if (includeSpecialtyCol)
        {
            return ReportResultRowViewModel.FromCells(
            [
                dateCell,
                dimension,
                specialtyCell ?? "—",
                metrics.Count,
                metrics.Average,
                metrics.Normative,
                metrics.Deviation,
                metrics.Min,
                metrics.Max
            ]);
        }

        return ReportResultRowViewModel.FromCells(
        [
            dateCell,
            dimension,
            metrics.Count,
            metrics.Average,
            metrics.Normative,
            metrics.Deviation,
            metrics.Min,
            metrics.Max
        ]);
    }

    private static readonly int[] PeriodTotalsLabelColSpansDoctor = [3, 0, 0, 1, 1, 1, 1, 1, 1];
    private static readonly int[] PeriodTotalsLabelColSpansSlice = [2, 0, 1, 1, 1, 1, 1, 1];

    private static void ApplyPeriodTotalsAndPreview(
        ReportResultViewModel model,
        IReadOnlyList<DurationObservation> observations,
        string mode,
        ReportGenerationPurpose purpose)
    {
        var includeSpecialtyCol = mode == ModeDoctor;
        var periodHeading = CatalogReportPreviewHelper.PeriodTotalsLabel;
        var detailRows = model.Rows;

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
                [heading, "", "", "", "", "", "", "", ""],
                rowClass: "report-load-table__row--totals-start",
                cellColSpans: PeriodTotalsLabelColSpansDoctor);
        }
        else
        {
            yield return ReportResultRowViewModel.FromCells(
                [heading, "", "", "", "", "", "", ""],
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
                    ["", g.Key, specialtyCell ?? "—", metrics.Count, metrics.Average, metrics.Normative, metrics.Deviation, metrics.Min, metrics.Max],
                    rowClass: "report-load-table__row--period-total");
            }
            else
            {
                yield return ReportResultRowViewModel.FromCells(
                    ["", g.Key, metrics.Count, metrics.Average, metrics.Normative, metrics.Deviation, metrics.Min, metrics.Max],
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
            return new DurationMetrics("0", "—", "—", "—", "—", "—");

        var svc = items.Select(i => i.SvcMin).ToList();
        var norms = items.Select(i => (double)i.NormMinutes).ToList();
        var avg = svc.Average();
        var normAvg = norms.Average();
        var deviation = avg - normAvg;
        var appointmentCount = items.Select(i => i.IdAppointment).Distinct().Count();
        return new DurationMetrics(
            appointmentCount.ToString(CultureInfo.InvariantCulture),
            CatalogReportShared.FormatMetric(avg),
            CatalogReportShared.FormatMetric(normAvg),
            CatalogReportShared.FormatMetric(deviation),
            CatalogReportShared.FormatMetric(svc.Min()),
            CatalogReportShared.FormatMetric(svc.Max()));
    }

    private readonly record struct DurationMetrics(
        string Count,
        string Average,
        string Normative,
        string Deviation,
        string Min,
        string Max);

    internal readonly record struct DurationObservation(
        DateOnly Date,
        string DimensionLabel,
        int IdAppointment,
        double SvcMin,
        int NormMinutes,
        string? SpecialtyDefinition);
}
