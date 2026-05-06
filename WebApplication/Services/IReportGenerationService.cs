using WebApplication.Models;

namespace WebApplication.Services;

public interface IReportGenerationService
{
    IReadOnlyList<ReportSelectOption> GetCabinetOptions();
    IReadOnlyList<ReportSelectOption> GetDoctorOptions();
    IReadOnlyList<ReportSelectOption> GetCategoryOptions();
    ReportGenerateResponse Generate(ReportGenerateRequest request);
    (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request);
    ReportResultViewModel GenerateQueueSummary(QueueSummaryReportParametersViewModel parameters);
    ReportResultViewModel GenerateCabinetLoad(CabinetLoadReportParametersViewModel parameters);
    byte[] BuildMockCsv(string reportId);
}
