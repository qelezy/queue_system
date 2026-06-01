using WebApplication.Data;

namespace WebApplication.Services.Reports;

public interface IReportGenerator
{
    ReportGeneratorKind Kind { get; }

    ReportGenerateResponse Generate(
        ReportGenerateRequest request,
        ElectronicQueueDbContext queue,
        ReportGenerationPurpose purpose);
}
