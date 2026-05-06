using System.ComponentModel.DataAnnotations;

namespace WebApplication.Models;

public sealed class QueueSummaryReportParametersViewModel
{
    [Required]
    [DataType(DataType.Date)]
    public string DateFrom { get; set; } = "";

    [Required]
    [DataType(DataType.Date)]
    public string DateTo { get; set; } = "";

    public long? CabinetId { get; set; }
    public long? DoctorId { get; set; }

    public IReadOnlyList<ReportSelectOption> CabinetOptions { get; set; } = [];
    public IReadOnlyList<ReportSelectOption> DoctorOptions { get; set; } = [];
}
