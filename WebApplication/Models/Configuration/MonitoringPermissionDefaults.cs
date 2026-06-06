namespace WebApplication.Models.Configuration;

public static class MonitoringPermissionDefaults
{
    public static readonly IReadOnlyList<string> RequiredPermissionIds =
    [
        "dashboard.waiting",
        "dashboard.in-service",
        "dashboard.accepted-today",
        "dashboard.tickets-issued",
        "dashboard.queue-table",
        "dashboard.chart-cabinets-load",
    ];

    private static readonly IReadOnlyDictionary<string, DashboardUiBlock> UiBlockByPermissionId =
        new Dictionary<string, DashboardUiBlock>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard.waiting"] = DashboardUiBlock.WaitingCard,
            ["dashboard.in-service"] = DashboardUiBlock.InServiceCard,
            ["dashboard.accepted-today"] = DashboardUiBlock.AcceptedTodayCard,
            ["dashboard.tickets-issued"] = DashboardUiBlock.TicketsIssuedCard,
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
