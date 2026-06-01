
namespace WebApplication.Services.Users;

public interface IRolePermissionService
{
    
    Task SyncPermissionsAndSeedDefaultsAsync(CancellationToken cancellationToken = default);

    Task<AccessSettingsViewModel> GetAccessMatrixAsync(CancellationToken cancellationToken = default);

    Task SaveAccessMatrixAsync(IReadOnlyList<AccessMatrixSaveEntryDto> entries, CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetPermissionNamesForRoleAsync(string roleName, CancellationToken cancellationToken = default);

    DashboardUiVisibility BuildDashboardVisibility(IReadOnlySet<string> permissionNames);
}
