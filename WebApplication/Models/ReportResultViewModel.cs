namespace WebApplication.Models;

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
}

/// <summary>Одна серия grouped bar (например час суток).</summary>
public sealed class ReportPreviewChartDataset
{
    public string Label { get; set; } = "";
    public List<double> Values { get; set; } = new();

    /// <summary>Норматив по дням (параллельно <see cref="Values"/>); наложение на один столбец среза.</summary>
    public List<double>? NormValues { get; set; }

    /// <summary>Устаревающее; для новых отчётов использовать <see cref="NormValues"/>.</summary>
    public string? ChartSeriesType { get; set; }
}

/// <summary>
/// Результат генерации отчёта для предпросмотра (JSON) и экспорта (табличная часть).
/// </summary>
/// <remarks>
/// Подключение нового отчёта: запись в каталог (<c>Reports:Catalog</c>), право с id отчёта,
/// реализация генерации (<see cref="Services.Reports.IReportGenerator"/> или ветка в <c>ReportGenerationService</c>),
/// при необходимости поля формы в <c>reportCustomConfig</c> (<c>wwwroot/js/reports-index.js</c>).
/// Для JSON-предпросмотра (<see cref="ReportGenerationPurpose.JsonPreview"/>) таблица <see cref="Rows"/> может быть усечена;
/// поля <see cref="PreviewPieChart"/> и <see cref="PreviewCharts"/> должны заполняться из полных агрегатов источника, а не из усечённых строк таблицы.
/// </remarks>
public sealed class ReportResultViewModel
{
    public string GeneratedForReportId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string TableLayout { get; set; } = ReportTableLayouts.Standard;
    public string PdfOrientation { get; set; } = ReportPdfOrientations.Landscape;
    public string DetailRowKind { get; set; } = ReportDetailRowKinds.Standard;
    public string DownloadFileName { get; set; } = "";
    public List<string> ColumnHeaders { get; set; } = new();
    public List<ReportResultRowViewModel> Rows { get; set; } = new();

    /// <summary>Опционально: диаграмма (устаревающий формат; клиент читает <see cref="PreviewCharts"/> или это поле). Данные — от полных агрегатов, не от усечённой <see cref="Rows"/>.</summary>
    public ReportPreviewPieChart? PreviewPieChart { get; set; }

    /// <summary>Опционально: одна или несколько диаграмм предпросмотра; серия точек — от полных агрегатов, не от усечённой <see cref="Rows"/>.</summary>
    public List<ReportPreviewChartDescriptor>? PreviewCharts { get; set; }

    /// <summary>Число строк до обрезки предпросмотра; null если обрезка не выполнялась.</summary>
    public int? PreviewRowsTotal { get; set; }

    /// <summary>Максимум строк в предпросмотре (если задан <see cref="PreviewRowsTotal"/>).</summary>
    public int? PreviewRowLimit { get; set; }
}

public sealed class ReportResultRowViewModel
{
    public List<string> Cells { get; set; } = new();

    /// <summary>
    /// Параллельно <see cref="Cells"/>: сколько колонок таблицы занимает ячейка в HTML.
    /// Сумма значений должна совпадать с числом колонок отчёта; 0 — позиция только для CSV (колонка «вошла» в colspan предыдущей ячейки).
    /// null — у каждой ячейки colspan 1.
    /// </summary>
    public List<int>? CellColSpans { get; set; }

    /// <summary>CSS-класс для строки таблицы (предпросмотр / разметка), не участвует в CSV.</summary>
    public string? RowClass { get; set; }

    /// <summary>Сборка строки таблицы для генераторов отчётов.</summary>
    public static ReportResultRowViewModel FromCells(
        IEnumerable<string> cells,
        string? rowClass = null,
        IReadOnlyList<int>? cellColSpans = null) =>
        new()
        {
            Cells = cells.ToList(),
            RowClass = rowClass,
            CellColSpans = cellColSpans?.ToList()
        };
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
                AriaLabel = "Соотношение длительности обслуживания и простоя",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

    /// <summary>
    /// Doughnut (итоги периода) и groupedBar (по дням) для отчёта «Необслуженные и разрывы».
    /// </summary>
    public static List<ReportPreviewChartDescriptor>? ForUnservedChainBreaksCharts(
        int noShowCount,
        int incompleteCount,
        IReadOnlyList<string> dailyDayLabels,
        IReadOnlyList<double> noShowPerDay,
        IReadOnlyList<double> incompletePerDay)
    {
        var outList = new List<ReportPreviewChartDescriptor>();

        var pieLabels = new List<string>();
        var pieValues = new List<double>();
        if (noShowCount > 0)
        {
            pieLabels.Add("Неявок на приёмы");
            pieValues.Add(noShowCount);
        }

        if (incompleteCount > 0)
        {
            pieLabels.Add("Приёмов с незавершённым обслуживанием");
            pieValues.Add(incompleteCount);
        }

        if (pieLabels.Count > 0)
        {
            outList.Add(new ReportPreviewChartDescriptor
            {
                Kind = "doughnut",
                Labels = pieLabels,
                Values = pieValues,
                ValueUnit = "шт.",
                AriaLabel = "Неявки и незавершённое обслуживание за период",
                CanvasElementId = "report-preview-chart-0"
            });
        }

        var bar = ForUnservedChainBreaksDailyGroupedBar(
            dailyDayLabels,
            noShowPerDay,
            incompletePerDay);
        if (bar is not null)
            outList.AddRange(bar);

        return outList.Count == 0 ? null : outList;
    }

    /// <summary>Ось X — дни; серии: неявки; незавершённое обслуживание (шт.).</summary>
    public static List<ReportPreviewChartDescriptor>? ForUnservedChainBreaksDailyGroupedBar(
        IReadOnlyList<string> dayLabels,
        IReadOnlyList<double> noShowPerDay,
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
        var dsNo = Pad(noShowPerDay, n);
        var dsInc = Pad(incompletePerDay, n);

        if (dsNo.All(x => x <= 0) && dsInc.All(x => x <= 0))
            return null;

        return
        [
            new ReportPreviewChartDescriptor
            {
                Kind = "groupedBar",
                Labels = [..dayLabels],
                Datasets =
                [
                    new ReportPreviewChartDataset { Label = "Неявок на приёмы", Values = dsNo },
                    new ReportPreviewChartDataset { Label = "Приёмов с незавершённым обслуживанием", Values = dsInc }
                ],
                ValueUnit = "шт.",
                AriaLabel = "Неявки и незавершённое обслуживание по дням",
                CanvasElementId = "report-preview-chart-1"
            }
        ];
    }

    /// <summary>Doughnut по итоговым одно- и многоэтапным маршрутам за период (C = число этапов List_item на приём).</summary>
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

    /// <summary>Doughnut по исходам приёма за период: неявки; завершённый маршрут; незавершённое обслуживание (суммы колонок 4–6).</summary>
    public static List<ReportPreviewChartDescriptor>? ForArrivedCompletedAppointmentMix(
        int noShows,
        int completedRoute,
        int incomplete)
    {
        var labels = new List<string>();
        var values = new List<double>();
        if (noShows > 0)
        {
            labels.Add("Неявок на приёмы");
            values.Add(noShows);
        }

        if (completedRoute > 0)
        {
            labels.Add("Приёмов с завершённым маршрутом");
            values.Add(completedRoute);
        }

        if (incomplete > 0)
        {
            labels.Add("Приёмов с незавершённым обслуживанием");
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
                AriaLabel = "Зарегистрированные приёмы за период: неявки, завершённый маршрут, незавершённое обслуживание",
                CanvasElementId = "report-preview-chart-0"
            }
        ];
    }

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

        if (datasets.All(d => d.Values.All(v => v <= 0) &&
                              (d.NormValues is null || d.NormValues.All(v => v <= 0))))
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
