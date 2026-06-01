
namespace WebApplication.Services.Dashboard;

public interface IQueueDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<AppointmentCompletedStagesResponse?> GetRouteStagesAsync(
        int idAppointment,
        CancellationToken cancellationToken = default);
}
