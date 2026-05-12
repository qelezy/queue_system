using WebApplication.Dto;
using WebApplication.Models;

namespace WebApplication.Services;

public interface IRolePermissionService
{
    /// <summary>Синхронизирует строки permission и при пустых role_permission задаёт значения по умолчанию.</summary>
    Task SyncPermissionsAndSeedDefaultsAsync(CancellationToken cancellationToken = default);

    Task<AccessSettingsViewModel> GetAccessMatrixAsync(CancellationToken cancellationToken = default);

    Task SaveAccessMatrixAsync(IReadOnlyList<AccessMatrixSaveEntryDto> entries, CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetPermissionNamesForRoleAsync(string roleName, CancellationToken cancellationToken = default);

    DashboardUiVisibility BuildDashboardVisibility(IReadOnlySet<string> permissionNames);
}
