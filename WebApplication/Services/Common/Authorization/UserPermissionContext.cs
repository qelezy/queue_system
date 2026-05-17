using Microsoft.AspNetCore.Identity;
using WebApplication.Services;

namespace WebApplication.Services.Common.Authorization;

public sealed class UserPermissionContext : IUserPermissionContext
{
    private const string DefaultRole = "Dispatcher";

    private readonly UserManager<User> _userManager;
    private readonly IRolePermissionService _rolePermissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserPermissionContext(
        UserManager<User> userManager,
        IRolePermissionService rolePermissionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _rolePermissionService = rolePermissionService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> GetCurrentRoleNameAsync(CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null)
            return DefaultRole;

        var user = await _userManager.GetUserAsync(principal).ConfigureAwait(false);
        if (user is null)
            return DefaultRole;

        return (await _userManager.GetRolesAsync(user).ConfigureAwait(false)).FirstOrDefault()
            ?? DefaultRole;
    }

    public async Task<HashSet<string>> GetCurrentPermissionNamesAsync(CancellationToken cancellationToken = default)
    {
        var roleName = await GetCurrentRoleNameAsync(cancellationToken).ConfigureAwait(false);
        return await _rolePermissionService.GetPermissionNamesForRoleAsync(roleName, cancellationToken)
            .ConfigureAwait(false);
    }
}
