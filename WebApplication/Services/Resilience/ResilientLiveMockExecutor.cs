using WebApplication.Services.Dashboard;

namespace WebApplication.Services.Resilience;

internal static class ResilientLiveMockExecutor
{
    internal static T TryLiveOrMock<T>(
        IElectronicQueueAvailability availability,
        Func<T> live,
        Func<T> mock)
    {
        if (!availability.TryGetCachedAvailability(out var ok) || !ok)
            return mock();

        Exception? liveFailure = null;
        try
        {
            return live();
        }
        catch (Exception ex)
        {
            liveFailure = ex;
            availability.MarkUnavailable();
        }

        try
        {
            return mock();
        }
        catch (Exception mockEx) when (liveFailure is not null)
        {
            throw new InvalidOperationException(
                "Не удалось сформировать отчёт по демо-данным после сбоя подключения к БД.",
                mockEx);
        }
    }
}
