using System.ComponentModel.DataAnnotations;

namespace WebApplication.Models;

public sealed class CabinetLoadReportParametersViewModel
{
    [Required]
    [DataType(DataType.Date)]
    public string WeekStart { get; set; } = "";
}
