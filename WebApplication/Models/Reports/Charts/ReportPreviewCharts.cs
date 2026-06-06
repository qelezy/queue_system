using System.Text.Json.Serialization;
using WebApplication.Services.Reports.Charts;

namespace WebApplication.Models.Reports.Charts;

public sealed class ReportPreviewPieChart
{
    public List<string> Labels { get; set; } = new();
    public List<double> Values { get; set; } = new();
}

public sealed class ReportPreviewChartDescriptor
{
    
    public string Kind { get; set; } = "doughnut";

    public List<string> Labels { get; set; } = new();
    public List<double> Values { get; set; } = new();

    public string? ValueUnit { get; set; }

    public string? AriaLabel { get; set; }

    public string? CanvasElementId { get; set; }

    public List<ReportPreviewChartDataset>? Datasets { get; set; }

    public string? ChartAxisMode { get; set; }
}

public sealed class ReportPreviewChartDataset
{
    public string Label { get; set; } = "";

    [JsonConverter(typeof(ChartDatasetValueListJsonConverter))]
    public List<double> Values { get; set; } = new();

    [JsonConverter(typeof(ChartDatasetValueListJsonConverter))]
    public List<double>? NormValues { get; set; }

    public string? ChartSeriesType { get; set; }
}

public static class ReportPreviewChartDescriptors
{
    public static List<ReportPreviewChartDescriptor>? ForLoadDowntimePie(ReportPreviewPieChart? pie)
    {
        if (pie is null)
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "doughnut",
                Labels = [..pie.Labels],
                Values = [..pie.Values],
                ValueUnit = "мин",
                AriaLabel = "Соотношение длительности занятости и простоев",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    public static List<ReportPreviewChartDescriptor>? ForServiceRouteOutcomesCharts(
        IReadOnlyList<string> dailyDayLabels,
        IReadOnlyList<double> completedPerDay,
        IReadOnlyList<double> incompletePerDay)
    {
        var bar = ForServiceRouteOutcomesDailyStackedBar(
            dailyDayLabels,
            completedPerDay,
            incompletePerDay);
        return bar;
    }

    public static List<ReportPreviewChartDescriptor>? ForServiceRouteOutcomesMix(
        int completedRoute,
        int incomplete)
    {
        var labels = new List<string>();
        var values = new List<double>();
        if (completedRoute > 0)
        {
            labels.Add("Полностью обслужено");
            values.Add(completedRoute);
        }

        if (incomplete > 0)
        {
            labels.Add("С незавершённым обслуживанием");
            values.Add(incomplete);
        }

        if (labels.Count == 0)
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "doughnut",
                Labels = labels,
                Values = values,
                ValueUnit = "приёмов",
                AriaLabel = "Исходы обслуживания за период: полностью и не полностью обслужено",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    public static List<ReportPreviewChartDescriptor>? ForServiceRouteOutcomesDailyStackedBar(
        IReadOnlyList<string> dayLabels,
        IReadOnlyList<double> completedPerDay,
        IReadOnlyList<double> incompletePerDay)
    {
        if (dayLabels.Count == 0)
            return null;

        static List<double> Pad(IReadOnlyList<double> v, int n)
        {
            var list = v.ToList();
            while (list.Count < n)
                list.Add(0);
            if (list.Count > n)
                list = list.Take(n).ToList();
            return list;
        }

        var n = dayLabels.Count;
        var dsCompleted = Pad(completedPerDay, n);
        var dsIncomplete = Pad(incompletePerDay, n);

        if (dsCompleted.All(x => x <= 0) && dsIncomplete.All(x => x <= 0))
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "groupedBar",
                ChartAxisMode = "stacked",
                Labels = [..dayLabels],
                Datasets =
                [
                    new ReportPreviewChartDataset { Label = "Полностью обслужено", Values = dsCompleted },
                    new ReportPreviewChartDataset { Label = "С незавершённым обслуживанием", Values = dsIncomplete }
                ],
                ValueUnit = "шт.",
                AriaLabel = "Исходы обслуживания по дням (завершённые и незавершённые)",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    public static List<ReportPreviewChartDescriptor>? ForServiceCategoriesComparisonHorizontalGroupedBar(
        IReadOnlyList<string> categoryLabels,
        IReadOnlyList<double?> avgWaitMinutes,
        IReadOnlyList<double?> avgSvcMinutes,
        IReadOnlyList<double?> avgTotalSvcMinutes)
    {
        if (categoryLabels.Count == 0)
            return null;

        var count = categoryLabels.Count;
        var waitValues = ToCategoryChartValues(avgWaitMinutes, count);
        var svcValues = ToCategoryChartValues(avgSvcMinutes, count);
        var totalSvcValues = ToCategoryChartValues(avgTotalSvcMinutes, count);
        var datasets = new List<ReportPreviewChartDataset>
        {
            new()
            {
                Label = "Среднее суммарное обслуживание",
                Values = totalSvcValues
            },
            new()
            {
                Label = "Среднее ожидание до вызова",
                Values = waitValues
            },
            new()
            {
                Label = "Средняя длительность приёма",
                Values = svcValues
            }
        };

        if (datasets.All(d => !ChartDatasetValues.HasFiniteValue(d.Values)))
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "horizontalGroupedBar",
                Labels = [..categoryLabels],
                Datasets = datasets,
                ValueUnit = "мин",
                AriaLabel = "Среднее ожидание и длительность приёма по категориям за период",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    private static List<double> ToCategoryChartValues(IReadOnlyList<double?> source, int count)
    {
        var list = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            var v = i < source.Count ? source[i] : null;
            list.Add(v is { } x && double.IsFinite(x) && x >= 0 ? x : ChartDatasetValues.Missing);
        }

        return list;
    }

    public static List<ReportPreviewChartDescriptor>? ForArrivedCompletedAppointmentMix(
        int completedRoute,
        int incomplete) =>
        ForServiceRouteOutcomesMix(completedRoute, incomplete);

    public static List<ReportPreviewChartDescriptor>? ForWaitingBeforeAppointmentDailyGroupedBar(
        IReadOnlyList<string> dayLabels,
        IReadOnlyList<ReportPreviewChartDataset> hourSeries)
    {
        if (dayLabels.Count == 0 || hourSeries.Count == 0)
            return null;

        var datasets = hourSeries
            .Select(h => new ReportPreviewChartDataset
            {
                Label = h.Label,
                Values = h.Values.Count == dayLabels.Count
                    ? [..h.Values]
                    : PadValues(h.Values, dayLabels.Count)
            })
            .ToList();

        if (datasets.All(d => !ChartDatasetValues.HasFiniteValue(d.Values)))
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "groupedBar",
                Labels = [..dayLabels],
                Datasets = datasets,
                ValueUnit = "мин",
                AriaLabel = "Среднее ожидание до вызова по дням и часам суток",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    public static List<ReportPreviewChartDescriptor>? ForAppointmentDurationPeriodHorizontalGroupedBar(
        IReadOnlyList<string> categoryLabels,
        IReadOnlyList<double?> avgMinutes,
        IReadOnlyList<double?> normMinutes,
        IReadOnlyList<double?> deviationMinutes)
    {
        if (categoryLabels.Count == 0)
            return null;

        var count = categoryLabels.Count;
        var datasets = new List<ReportPreviewChartDataset>
        {
            new()
            {
                Label = "Средняя длительность приёма",
                Values = ToCategoryChartValues(avgMinutes, count)
            },
            new()
            {
                Label = "Норматив",
                Values = ToCategoryChartValues(normMinutes, count)
            },
            new()
            {
                Label = "Отклонение",
                Values = ToSignedCategoryChartValues(deviationMinutes, count)
            }
        };

        if (datasets.All(d => !ChartDatasetValues.HasFiniteValue(d.Values)))
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "horizontalGroupedBar",
                ChartAxisMode = "symmetric",
                Labels = [..categoryLabels],
                Datasets = datasets,
                ValueUnit = "мин",
                AriaLabel = "Средняя длительность приёма, норматив и отклонение по срезу за период",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    private static List<double> ToSignedCategoryChartValues(IReadOnlyList<double?> source, int count)
    {
        var list = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            var v = i < source.Count ? source[i] : null;
            list.Add(v is { } x && double.IsFinite(x) && x != 0 ? x : ChartDatasetValues.Missing);
        }

        return list;
    }

    public static List<ReportPreviewChartDescriptor>? ForStagesAndWaitingDailyGroupedBar(
        IReadOnlyList<string> dayLabels,
        IReadOnlyList<ReportPreviewChartDataset> series)
    {
        if (dayLabels.Count == 0 || series.Count == 0)
            return null;

        var datasets = series
            .Select(s => new ReportPreviewChartDataset
            {
                Label = s.Label,
                Values = s.Values.Count == dayLabels.Count
                    ? [..s.Values]
                    : PadValues(s.Values, dayLabels.Count)
            })
            .ToList();

        if (datasets.All(d => d.Values.All(v => v <= 0)))
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "groupedBar",
                Labels = [..dayLabels],
                Datasets = datasets,
                ValueUnit = "мин",
                AriaLabel = "Среднее время обслуживания и ожидания после вызова по дням",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    private static List<double> PadValues(IReadOnlyList<double> values, int targetCount)
    {
        var list = values.ToList();
        while (list.Count < targetCount)
            list.Add(0);
        if (list.Count > targetCount)
            list = list.Take(targetCount).ToList();
        return list;
    }
}
