using WebApplication.Services.Dashboard;

namespace WebApplication.Services.Resilience;

internal static class ResilientLiveMockExecutor
{
    internal const string ExportUnavailableMessage =
        "Нет подключения к базе данных очереди. Экспорт полного отчёта недоступен.";

    internal static T TryLiveOrMockForExport<T>(
        IElectronicQueueAvailability availability,
        bool allowMockFallback,
        Func<T> live,
        Func<T> mock)
    {
        if (!availability.TryGetCachedAvailability(out var ok) || !ok)
        {
            if (!allowMockFallback)
                throw new InvalidOperationException(ExportUnavailableMessage);

            return mock();
        }

        try
        {
            return live();
        }
        catch (Exception ex)
        {
            availability.MarkUnavailable();
            if (!allowMockFallback)
            {
                throw new InvalidOperationException(
                    "Не удалось сформировать файл экспорта.",
                    ex);
            }

            return mock();
        }
    }

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
                "Не удалось сформировать отчёт после сбоя подключения к БД очереди.",
                mockEx);
        }
    }
}
