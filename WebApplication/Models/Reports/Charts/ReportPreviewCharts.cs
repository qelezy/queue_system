using System.Text.Json.Serialization;

namespace WebApplication.Models.Reports.Charts;

/// <summary>
/// Данные для круговой диаграммы в предпросмотре; при экспорте в CSV отражаются в блоке перед таблицей.
/// Предпочтительно задавать вместе с <see cref="ReportResultViewModel.PreviewCharts"/>; свойство сохранено для совместимости.
/// </summary>
public sealed class ReportPreviewPieChart
{
    public List<string> Labels { get; set; } = new();
    public List<double> Values { get; set; } = new();
}

/// <summary>
/// Описание диаграммы для предпросмотра; при экспорте в CSV данные диаграммы выводятся отдельным блоком перед таблицей.
/// </summary>
public sealed class ReportPreviewChartDescriptor
{
    /// <summary>Вид диаграммы: <c>doughnut</c>, <c>pie</c> и т.д. (регистрация кастомных видов на клиенте).</summary>
    public string Kind { get; set; } = "doughnut";

    public List<string> Labels { get; set; } = new();
    public List<double> Values { get; set; } = new();

    /// <summary>Единица для подсказки (например «мин»); пусто — только число.</summary>
    public string? ValueUnit { get; set; }

    public string? AriaLabel { get; set; }

    /// <summary>Уникальный id элемента canvas; по умолчанию на клиенте — report-preview-chart-индекс.</summary>
    public string? CanvasElementId { get; set; }

    /// <summary>Серии для <c>groupedBar</c>: подпись (час) и значения по дням (ось X — <see cref="Labels"/>).</summary>
    public List<ReportPreviewChartDataset>? Datasets { get; set; }

    /// <summary>Сноска под диаграммой (например при агрегации оси X по неделям).</summary>
    public string? Footnote { get; set; }
}

/// <summary>Одна серия grouped bar (например час суток).</summary>
public sealed class ReportPreviewChartDataset
{
    public string Label { get; set; } = "";

    [JsonConverter(typeof(ChartDatasetValueListJsonConverter))]
    public List<double> Values { get; set; } = new();

    /// <summary>Норматив по дням (параллельно <see cref="Values"/>); наложение на один столбец среза.</summary>
    [JsonConverter(typeof(ChartDatasetValueListJsonConverter))]
    public List<double>? NormValues { get; set; }

    /// <summary>Устаревающее; для новых отчётов использовать <see cref="NormValues"/>.</summary>
    public string? ChartSeriesType { get; set; }
}

/// <summary>Готовые дескрипторы предпросмотра для конкретных отчётов.</summary>
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

    /// <summary>Doughnut (итоги) и groupedBar (по дням) для отчёта «Исходы обслуживания».</summary>
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

    /// <summary>Doughnut по исходам талонов за период: завершённый маршрут; незавершённое обслуживание.</summary>
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

    /// <summary>Ось X — дни; серии: завершённый маршрут; незавершённое обслуживание (шт.).</summary>
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


    /// <summary>Устаревший alias — делегирует в <see cref="ForServiceRouteOutcomesMix"/>.</summary>
    public static List<ReportPreviewChartDescriptor>? ForArrivedCompletedAppointmentMix(
        int completedRoute,
        int incomplete) =>
        ForServiceRouteOutcomesMix(completedRoute, incomplete);

    /// <summary>Grouped bar: по оси X — дни, серии — часы 00:00–23:00, значение — среднее ожидание (мин).</summary>
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

    /// <summary>Grouped bar: по оси X — дни, серии — топ значений среза, значение — средняя длительность (мин).</summary>
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

    /// <summary>Grouped bar: по оси X — дни, серии — суммы прохождения и пауз по дню.</summary>
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
