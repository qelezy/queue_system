using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services;

/// <summary>
/// Человекочитаемые статусы этапа очереди для мониторинга (см. queue-monitoring-page-ui-requirements.md).
/// </summary>
public static class QueueDashboardStatusMapper
{
    /// <summary>Текущий этап талона: неявка из БД; иначе эвристика по временным меткам.</summary>
    public static (string Label, string Code) ResolveForCurrentStep(EqListItem li)
    {
        var db = MapFromDbName(li.StatusItem?.Name);
        if (db.Code == "no-show")
            return db;

        if (li.TimeEndServicing.HasValue)
            return ("Завершён", "done");
        if (li.TimeStartServicing.HasValue)
            return ("На приёме", "in-service");
        if (li.TimeCall.HasValue)
            return ("Вызван", "called");
        return ("Ожидает", "waiting");
    }

    public static bool IsWaitingOrCalledCode(string code) =>
        code is "waiting" or "called";

    public static bool IsNoShowStatusName(string? statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
            return false;
        var n = statusName.Trim().ToLowerInvariant();
        return n.Contains("неяв", StringComparison.Ordinal)
               || n.Contains("не яв", StringComparison.Ordinal)
               || n.Contains("no-show", StringComparison.Ordinal)
               || n.Contains("noshow", StringComparison.Ordinal)
               || n.Contains("пропуск", StringComparison.Ordinal);
    }

    private static (string Label, string Code) MapFromDbName(string? statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
            return ("Ожидает", "waiting");

        var n = statusName.Trim().ToLowerInvariant();
        if (IsNoShowStatusName(n))
            return ("Не явился", "no-show");
        if (ContainsAny(n, "заверш", "complete", "окончен", "выполн"))
            return ("Завершён", "done");
        if (ContainsAny(n, "прием", "приём", "обслуж", "servicing"))
            return ("На приёме", "in-service");
        if (ContainsAny(n, "вызов", "called"))
            return ("Вызван", "called");
        if (ContainsAny(n, "ожид", "wait", "очеред"))
            return ("Ожидает", "waiting");

        return (statusName.Trim(), "waiting");
    }

    private static bool ContainsAny(string normalizedLower, params string[] parts)
    {
        foreach (var p in parts)
        {
            if (normalizedLower.Contains(p, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
