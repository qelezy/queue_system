namespace WebApplication.Models;

/// <summary>Общие ограничения предпросмотра отчётов (JSON).</summary>
public static class ReportPreviewLimits
{
    /// <summary>Максимум строк таблицы в ответе предпросмотра (включая служебные строки-хвост при усечении в генераторе).</summary>
    public const int MaxTableRows = 500;
}
