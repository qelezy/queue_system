namespace WebApplication.Models;

/// <summary>Данные для круговой диаграммы в предпросмотре (не участвует в CSV).</summary>
public sealed class ReportPreviewPieChart
{
    public List<string> Labels { get; set; } = new();
    public List<double> Values { get; set; } = new();
}

public sealed class ReportResultViewModel
{
    public string GeneratedForReportId { get; set; } = "";
    public string Title { get; set; } = "";
    public string DownloadFileName { get; set; } = "";
    public List<string> ColumnHeaders { get; set; } = new();
    public List<ReportResultRowViewModel> Rows { get; set; } = new();

    /// <summary>Опционально: диаграмма для предпросмотра (например «Загрузка и простои»).</summary>
    public ReportPreviewPieChart? PreviewPieChart { get; set; }
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
}
