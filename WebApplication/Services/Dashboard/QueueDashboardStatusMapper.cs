using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Dashboard;

public static class QueueDashboardStatusMapper
{
    public static (string Label, string Code) ResolveForCurrentStep(EqListItem li)
    {
        var db = MapFromDbName(li.StatusItem?.Name);
        if (li.TimeEndServicing.HasValue)
            return ("Завершён", "done");
        if (li.TimeStartServicing.HasValue)
            return ("На приёме", "in-service");
        if (li.TimeCall.HasValue)
            return ("Вызван", "called");
        if (db.Code != "waiting")
            return db;
        return ("Ожидает", "waiting");
    }

    public static bool IsWaitingOrCalledCode(string code) =>
        code is "waiting" or "called";

    public static bool IsInServiceStep(EqListItem li)
    {
        if (IsExcludedStatusItem(li))
            return false;
        var (_, code) = ResolveForCurrentStep(li);
        return code == "in-service";
    }

    public static bool IsExcludedStatusName(string? statusName)
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

    public static bool IsExcludedStatusItem(EqListItem li) =>
        IsExcludedStatusName(li.StatusItem?.Name);

    public static bool IsWaitingQueueStep(EqListItem li)
    {
        if (li.TimeCall.HasValue || IsExcludedStatusItem(li))
            return false;
        var (_, code) = ResolveForCurrentStep(li);
        return code == "waiting";
    }

    public static bool IsWaitingListStep(EqListItem li)
    {
        if (IsExcludedStatusItem(li) || li.TimeStartServicing.HasValue)
            return false;
        var (_, code) = ResolveForCurrentStep(li);
        return IsWaitingOrCalledCode(code);
    }

    private static (string Label, string Code) MapFromDbName(string? statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
            return ("Ожидает", "waiting");

        var n = statusName.Trim().ToLowerInvariant();
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
