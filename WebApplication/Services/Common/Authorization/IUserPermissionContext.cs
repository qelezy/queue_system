namespace WebApplication.Services.Common.Authorization;

public interface IUserPermissionContext
{
    Task<string> GetCurrentRoleNameAsync(CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetCurrentPermissionNamesAsync(CancellationToken cancellationToken = default);
}
