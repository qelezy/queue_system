namespace WebApplication.Services;

public interface IElectronicQueueAvailability
{
    /// <summary>Проверяет доступность БД очереди (с кэшированием).</summary>
    Task<bool> CanQueryLiveDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Читает последний закэшированный результат; false если кэша ещё нет.</summary>
    bool TryGetCachedAvailability(out bool canConnectLive);
}
