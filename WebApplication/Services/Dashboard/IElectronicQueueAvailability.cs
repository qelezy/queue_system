namespace WebApplication.Services.Dashboard;

public interface IElectronicQueueAvailability
{
    
    Task<bool> CanQueryLiveDataAsync(CancellationToken cancellationToken = default);

    bool TryGetCachedAvailability(out bool canConnectLive);

    void MarkUnavailable();

    void MarkAvailable();
}
