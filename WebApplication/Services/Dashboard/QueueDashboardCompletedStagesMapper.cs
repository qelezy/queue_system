using System.Globalization;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Dashboard;

internal static class QueueDashboardCompletedStagesMapper
{
    internal static bool IsCompletedStage(EqListItem li) =>
        li.TimeEndServicing.HasValue;

    internal static bool IsRouteStage(EqListItem li) => true;

    internal static AppointmentCompletedStageDto ToDto(EqListItem li)
    {
        var specialty = li.Specialty?.Definition?.Trim();
        var cabinet = li.Cabinet?.CabinetNumber?.Trim();
        return new AppointmentCompletedStageDto
        {
            Specialty = string.IsNullOrWhiteSpace(specialty) ? "—" : specialty,
            Cabinet = string.IsNullOrWhiteSpace(cabinet) ? "—" : cabinet,
            TimeCall = FormatTime(li.TimeCall),
            TimeStart = FormatTime(li.TimeStartServicing),
            TimeEnd = FormatTime(li.TimeEndServicing),
        };
    }

    private static string FormatTime(TimeOnly? time) =>
        time.HasValue ? time.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "—";
}
