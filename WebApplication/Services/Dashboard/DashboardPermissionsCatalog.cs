using Microsoft.Extensions.Options;
using WebApplication.Models.Configuration;
using WebApplication.Models.ViewModels.Dashboard;

namespace WebApplication.Services.Dashboard;

public sealed class DashboardPermissionsCatalog : IDashboardPermissionsCatalog
{
    private readonly IOptions<MonitoringOptions> _options;
    private IReadOnlyList<MonitoringPermissionItemViewModel>? _cache;

    public DashboardPermissionsCatalog(IOptions<MonitoringOptions> options)
    {
        _options = options;
    }

    public IReadOnlyList<MonitoringPermissionItemViewModel> GetPermissions()
    {
        if (_cache is not null)
            return _cache;

        _cache = _options.Value.Permissions
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .Select(Map)
            .ToList();

        return _cache;
    }

    private static MonitoringPermissionItemViewModel Map(MonitoringPermissionOptions p) =>
        new()
        {
            Id = p.Id.Trim(),
            Title = p.Title ?? "",
            Description = p.Description ?? "",
        };
}
