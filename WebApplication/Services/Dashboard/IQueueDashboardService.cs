
namespace WebApplication.Services.Dashboard;

public interface IQueueDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default);
}
