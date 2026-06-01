using Microsoft.Extensions.Options;
using WebApplication.Models.Configuration;

namespace WebApplication.Services.Dashboard;

public static class MonitoringConfigurationValidator
{
    public static void Validate(IOptions<MonitoringOptions> options, IDashboardPermissionsCatalog catalog)
    {
        var raw = options.Value.Permissions;
        if (raw.Count == 0)
        {
            throw new InvalidOperationException(
                "Monitoring:Permissions не задан. Добавьте блоки мониторинга в appsettings.json.");
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in raw)
        {
            var id = (p.Id ?? "").Trim();
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException("Monitoring:Permissions содержит элемент с пустым Id.");

            if (!id.StartsWith("dashboard.", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Monitoring:Permissions: Id «{id}» должен начинаться с «dashboard.».");
            }

            if (string.IsNullOrWhiteSpace(p.Title))
            {
                throw new InvalidOperationException(
                    $"Monitoring:Permissions: пустой Title для «{id}».");
            }

            if (string.IsNullOrWhiteSpace(p.Description))
            {
                throw new InvalidOperationException(
                    $"Monitoring:Permissions: пустой Description для «{id}».");
            }

            if (!MonitoringPermissionDefaults.TryResolveUiBlock(id, out _))
            {
                throw new InvalidOperationException(
                    $"Monitoring:Permissions: неизвестный Id «{id}» (нет сопоставления с блоком UI).");
            }

            if (!seenIds.Add(id))
            {
                throw new InvalidOperationException(
                    $"Monitoring:Permissions: дублирующийся Id «{id}».");
            }
        }

        foreach (var requiredId in MonitoringPermissionDefaults.RequiredPermissionIds)
        {
            if (!seenIds.Contains(requiredId))
            {
                throw new InvalidOperationException(
                    $"Monitoring:Permissions: отсутствует обязательный Id «{requiredId}».");
            }
        }

        if (catalog.GetPermissions().Count != raw.Count)
        {
            throw new InvalidOperationException(
                "Monitoring:Permissions: не все элементы каталога сопоставлены (проверьте Id).");
        }
    }
}
