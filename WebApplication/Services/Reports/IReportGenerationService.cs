
namespace WebApplication.Services.Reports;

public interface IReportGenerationService
{
    IReadOnlyList<ReportSelectOption> GetCabinetOptions();
    IReadOnlyList<ReportSelectOption> GetDoctorOptions();
    IReadOnlyList<ReportSelectOption> GetCategoryOptions();
    
    ReportGenerateResponse Generate(ReportGenerateRequest request, ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull);
    (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request);
}
