using WebApplication.Models;

namespace WebApplication.Services;

public interface IQueueDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default);
}
