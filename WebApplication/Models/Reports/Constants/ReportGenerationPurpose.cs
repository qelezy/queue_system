namespace WebApplication.Models.Reports.Constants;

/// <summary>
/// Зачем вызывается генерация: JSON-предпросмотр в браузере или полный результат (экспорт файла, CSV-заглушки).
/// </summary>
public enum ReportGenerationPurpose
{
    /// <summary>Ответ <c>/Reports/Generate</c>: таблица может быть усечена; графики — от полных агрегатов источника.</summary>
    JsonPreview,

    /// <summary>Полная таблица и агрегаты (экспорт, внутренние вызовы без лимита строк).</summary>
    ExportOrFull
}
