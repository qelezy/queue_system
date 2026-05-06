using WebApplication.Models;

namespace WebApplication.Services;

public interface IQueueAnalyticsService
{
    Task<ManagerAnalyticsViewModel> GetManagerAnalyticsAsync(
        DateOnly from,
        DateOnly to,
        int? cabinetId,
        int? doctorId,
        int? categoryId,
        bool heatmapByDoctor,
        CancellationToken cancellationToken = default);
}
