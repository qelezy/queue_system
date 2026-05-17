namespace WebApplication.Services.Dashboard;

public interface IElectronicQueueAvailability
{
    /// <summary>Проверяет доступность БД очереди (с кэшированием).</summary>
    Task<bool> CanQueryLiveDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Читает последний закэшированный результат; false если кэша ещё нет.</summary>
    bool TryGetCachedAvailability(out bool canConnectLive);

    /// <summary>Помечает БД недоступной в кэше (после сбоя live-запроса).</summary>
    void MarkUnavailable();
}
