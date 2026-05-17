using System.ComponentModel.DataAnnotations;

namespace WebApplication.Models;

/// <summary>
/// Тело POST для <c>/Reports/Generate</c> и <c>/Reports/Export</c>. Доп. поля отчёта — в <see cref="CustomParams"/>
/// и/или встроенных свойствах; на клиенте источник значений — форма и <c>reportCustomConfig</c>.
/// Контроллер вызывает генерацию с <see cref="ReportGenerationPurpose.JsonPreview"/> только для <c>/Reports/Generate</c>;
/// для <c>/Reports/Export</c> используется <see cref="ReportGenerationPurpose.ExportOrFull"/> (полная таблица в файле).
/// </summary>
public class ReportGenerateRequest
{
    [Required]
    public string ReportId { get; set; } = "";
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public string? WeekStart { get; set; }
    public long? CabinetId { get; set; }
    public long? DoctorId { get; set; }
    public Dictionary<string, string?> CustomParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ReportGenerateResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool Implemented { get; set; }
    public ReportResultViewModel? Result { get; set; }
}

public sealed class ReportExportRequest : ReportGenerateRequest
{
    [Required]
    public string Format { get; set; } = "csv";
}
