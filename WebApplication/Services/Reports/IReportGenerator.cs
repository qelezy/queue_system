using WebApplication.Data;
using WebApplication.Models;

namespace WebApplication.Services.Reports;

public interface IReportGenerator
{
    string ReportId { get; }

    ReportGenerateResponse Generate(ReportGenerateRequest request, ElectronicQueueDbContext queue);
}
