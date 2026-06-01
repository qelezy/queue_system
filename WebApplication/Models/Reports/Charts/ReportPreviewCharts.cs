using System.Text.Json.Serialization;

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
                AriaLabel = "Соотношение длительности занятости и простоя",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    public static List<ReportPreviewChartDescriptor>? ForServiceRouteOutcomesCharts(
        int completedCount,
        int incompleteCount,
        IReadOnlyList<string> dailyDayLabels,
        IReadOnlyList<double> completedPerDay,
        IReadOnlyList<double> incompletePerDay)
    {
        var outList = new List<ReportPreviewChartDescriptor>();

        var doughnut = ForServiceRouteOutcomesMix(completedCount, incompleteCount);
        if (doughnut is not null)
            outList.AddRange(doughnut);

        var bar = ForServiceRouteOutcomesDailyGroupedBar(
            dailyDayLabels,
            completedPerDay,
            incompletePerDay);
        if (bar is not null)
            outList.AddRange(bar);

        return outList.Count == 0 ? null : outList;
    }

    public static List<ReportPreviewChartDescriptor>? ForServiceRouteOutcomesMix(
        int completedRoute,
        int incomplete)
    {
        var labels = new List<string>();
        var values = new List<double>();
        if (completedRoute > 0)
        {
            labels.Add("С завершённым маршрутом");
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
                AriaLabel = "Исходы обслуживания за период: завершённый маршрут и незавершённое обслуживание",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    public static List<ReportPreviewChartDescriptor>? ForServiceRouteOutcomesDailyGroupedBar(
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
                Labels = [..dayLabels],
                Datasets =
                [
                    new ReportPreviewChartDataset { Label = "С завершённым маршрутом", Values = dsCompleted },
                    new ReportPreviewChartDataset { Label = "С незавершённым обслуживанием", Values = dsIncomplete }
                ],
                ValueUnit = "шт.",
                AriaLabel = "Исходы обслуживания по дням",
                CanvasElementId = "report-preview-chart-1"
            }
        ];
    }

    public static List<ReportPreviewChartDescriptor>? ForMultiStageRoutesMix(int single, int multi)
    {
        var labels = new List<string>();
        var values = new List<double>();
        if (single > 0)
        {
            labels.Add("Одноэтапные маршруты");
            values.Add(single);
        }

        if (multi > 0)
        {
            labels.Add("Многоэтапные маршруты");
            values.Add(multi);
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
                ValueUnit = "маршрутов",
                AriaLabel = "Маршруты за период: одноэтапные и многоэтапные по числу этапов",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
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

    public static List<ReportPreviewChartDescriptor>? ForAppointmentDurationDailyGroupedBar(
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
                    : PadValues(s.Values, dayLabels.Count),
                NormValues = s.NormValues is null
                    ? null
                    : s.NormValues.Count == dayLabels.Count
                        ? [..s.NormValues]
                        : PadValues(s.NormValues, dayLabels.Count)
            })
            .ToList();

        if (datasets.All(d => !ChartDatasetValues.HasFiniteValue(d.Values)
                              && (d.NormValues is null || !ChartDatasetValues.HasFiniteValue(d.NormValues))))
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "groupedBar",
                Labels = [..dayLabels],
                Datasets = datasets,
                ValueUnit = "мин",
                AriaLabel = "Средняя длительность приёма и норматив по дням и срезу",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    public static List<ReportPreviewChartDescriptor>? ForRouteAndPausesDailyGroupedBar(
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
                AriaLabel = "Суммы времени прохождения и пауз по дням",
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
