using WebApplication.Models;

namespace WebApplication.Services;

public interface IReportGenerationService
{
    IReadOnlyList<ReportSelectOption> GetCabinetOptions();
    IReadOnlyList<ReportSelectOption> GetDoctorOptions();
    IReadOnlyList<ReportSelectOption> GetCategoryOptions();
    /// <param name="purpose"><see cref="ReportGenerationPurpose.JsonPreview"/> для <c>/Reports/Generate</c>; <see cref="ReportGenerationPurpose.ExportOrFull"/> для экспорта и полных пересчётов.</param>
    ReportGenerateResponse Generate(ReportGenerateRequest request, ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull);
    (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request);
    byte[] BuildMockCsv(string reportId, string? analysisMode = null);
}
