using WebApplication.Models.ViewModels.Dashboard;

namespace WebApplication.Services.Dashboard;

public interface IDashboardPermissionsCatalog
{
    IReadOnlyList<MonitoringPermissionItemViewModel> GetPermissions();
}
