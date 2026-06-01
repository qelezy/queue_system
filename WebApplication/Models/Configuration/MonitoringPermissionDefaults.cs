namespace WebApplication.Models.Configuration;

public static class MonitoringPermissionDefaults
{
    public static readonly IReadOnlyList<string> RequiredPermissionIds =
    [
        "dashboard.waiting",
        "dashboard.in-service",
        "dashboard.accepted-today",
        "dashboard.avg-wait",
        "dashboard.avg-service",
        "dashboard.chart-cabinets-load",
        "dashboard.queue-table",
    ];

    private static readonly IReadOnlyDictionary<string, DashboardUiBlock> UiBlockByPermissionId =
        new Dictionary<string, DashboardUiBlock>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard.waiting"] = DashboardUiBlock.WaitingCard,
            ["dashboard.in-service"] = DashboardUiBlock.InServiceCard,
            ["dashboard.accepted-today"] = DashboardUiBlock.AcceptedTodayCard,
            ["dashboard.avg-wait"] = DashboardUiBlock.AvgWaitCard,
            ["dashboard.avg-service"] = DashboardUiBlock.AvgServiceCard,
            ["dashboard.chart-cabinets-load"] = DashboardUiBlock.DoctorLoad,
            ["dashboard.queue-table"] = DashboardUiBlock.QueueTable,
        };

    public static bool TryResolveUiBlock(string? permissionId, out DashboardUiBlock block)
    {
        block = default;
        if (string.IsNullOrWhiteSpace(permissionId))
            return false;

        return UiBlockByPermissionId.TryGetValue(permissionId.Trim(), out block);
    }
}
