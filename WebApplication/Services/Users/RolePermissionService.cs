using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApplication.Data;
using WebApplication.Models.Configuration;
using WebApplication.Services.Dashboard;
using WebApplication.Services.Reports;

namespace WebApplication.Services.Users;

public sealed class RolePermissionService : IRolePermissionService
{
    private static readonly string[] MatrixRoleNames = ["Admin", "Manager", "Dispatcher"];

    private readonly AppDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IReportsCatalog _reportsCatalog;
    private readonly IDashboardPermissionsCatalog _permissionsCatalog;
    private readonly ILogger<RolePermissionService> _logger;

    public RolePermissionService(
        AppDbContext db,
        RoleManager<IdentityRole> roleManager,
        IReportsCatalog reportsCatalog,
        IDashboardPermissionsCatalog permissionsCatalog,
        ILogger<RolePermissionService> logger)
    {
        _db = db;
        _roleManager = roleManager;
        _reportsCatalog = reportsCatalog;
        _permissionsCatalog = permissionsCatalog;
        _logger = logger;
    }

    public async Task SyncPermissionsAndSeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionRowsAsync(cancellationToken).ConfigureAwait(false);
        await RemoveOrphanPermissionsAsync(cancellationToken).ConfigureAwait(false);
        await SeedDefaultRoleLinksIfEmptyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsurePermissionRowsAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Permissions
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _permissionsCatalog.GetPermissions())
        {
            if (set.Add(item.Id))
                _db.Permissions.Add(new Permission { PermissionName = item.Id });
        }

        foreach (var item in _reportsCatalog.GetCatalog())
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !set.Add(item.Id))
                continue;
            _db.Permissions.Add(new Permission { PermissionName = item.Id.Trim() });
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveOrphanPermissionsAsync(CancellationToken cancellationToken)
    {
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _permissionsCatalog.GetPermissions())
            knownIds.Add(item.Id);

        foreach (var item in _reportsCatalog.GetCatalog())
        {
            if (!string.IsNullOrWhiteSpace(item.Id))
                knownIds.Add(item.Id.Trim());
        }

        var orphans = await _db.Permissions
            .Where(p => !knownIds.Contains(p.PermissionName))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (orphans.Count == 0)
            return;

        foreach (var orphan in orphans)
        {
            await _db.RolePermissions
                .Where(rp => rp.PermissionId == orphan.PermissionId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            _db.Permissions.Remove(orphan);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Удалены устаревшие permissions ({Count}): {Names}",
            orphans.Count,
            string.Join(", ", orphans.Select(p => p.PermissionName)));
    }

    private static bool DefaultGrant(string roleName, string permissionName)
    {
        if (permissionName.StartsWith("dashboard.", StringComparison.OrdinalIgnoreCase))
            return true;
        return !string.Equals(roleName, "Dispatcher", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedDefaultRoleLinksIfEmptyAsync(CancellationToken cancellationToken)
    {
        var allPermissions = await _db.Permissions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        if (allPermissions.Count == 0)
            return;

        foreach (var roleName in MatrixRoleNames)
        {
            var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
            if (role == null)
                continue;

            var hasAny = await _db.RolePermissions.AsNoTracking()
                .AnyAsync(rp => rp.RoleId == role.Id, cancellationToken)
                .ConfigureAwait(false);
            if (hasAny)
                continue;

            foreach (var p in allPermissions)
            {
                if (!DefaultGrant(roleName, p.PermissionName))
                    continue;
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = p.PermissionId,
                });
            }
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccessSettingsViewModel> GetAccessMatrixAsync(CancellationToken cancellationToken = default)
    {
        await SyncPermissionsAndSeedDefaultsAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<AccessRoleColumn> roleColumns =
        [
            new("Admin", "Администратор"),
            new("Manager", "Менеджер"),
            new("Dispatcher", "Диспетчер"),
        ];

        var roleIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rn in MatrixRoleNames)
        {
            var r = await _roleManager.FindByNameAsync(rn).ConfigureAwait(false);
            if (r != null)
                roleIds[rn] = r.Id;
        }

        var grantedList = await _db.RolePermissions.AsNoTracking()
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var grantedSet = grantedList.Select(x => (x.RoleId, x.PermissionId)).ToHashSet();

        bool IsGranted(string roleName, int permissionId)
        {
            if (!roleIds.TryGetValue(roleName, out var rid))
                return false;
            return grantedSet.Contains((rid, permissionId));
        }

        var permByName = await _db.Permissions.AsNoTracking()
            .ToDictionaryAsync(x => x.PermissionName, x => x.PermissionId, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var vizItems = new List<AccessItemViewModel>();
        foreach (var item in _permissionsCatalog.GetPermissions())
        {
            if (!permByName.TryGetValue(item.Id, out var pid))
                continue;

            vizItems.Add(new AccessItemViewModel
            {
                Key = item.Id,
                Title = item.Title,
                Description = item.Description,
                RolePermissions = MatrixRoleNames.ToDictionary(
                    rn => rn,
                    rn => IsGranted(rn, pid),
                    StringComparer.OrdinalIgnoreCase),
            });
        }

        var reportItems = new List<AccessItemViewModel>();
        foreach (var item in _reportsCatalog.GetCatalog())
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                continue;
            var id = item.Id.Trim();
            if (!permByName.TryGetValue(id, out var pid))
                continue;

            reportItems.Add(new AccessItemViewModel
            {
                Key = id,
                Title = string.IsNullOrWhiteSpace(item.Title) ? item.Id : item.Title,
                Description = item.Description ?? "",
                RolePermissions = MatrixRoleNames.ToDictionary(
                    rn => rn,
                    rn => IsGranted(rn, pid),
                    StringComparer.OrdinalIgnoreCase),
            });
        }

        IReadOnlyList<AccessGroupViewModel> groups =
        [
            new()
            {
                Key = "viz",
                Title = "Визуализации мониторинга",
                Icon = "bi-graph-up-arrow",
                Items = vizItems,
            },
            new()
            {
                Key = "report",
                Title = "Отчёты",
                Icon = "bi-journal-text",
                Items = reportItems,
            },
        ];

        return new AccessSettingsViewModel
        {
            Roles = roleColumns,
            Groups = groups,
        };
    }

    public async Task SaveAccessMatrixAsync(IReadOnlyList<AccessMatrixSaveEntryDto> entries, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionRowsAsync(cancellationToken).ConfigureAwait(false);

        var permissionByName = await _db.Permissions.AsNoTracking()
            .ToDictionaryAsync(p => p.PermissionName, p => p.PermissionId, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var roleIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rn in MatrixRoleNames)
        {
            var r = await _roleManager.FindByNameAsync(rn).ConfigureAwait(false);
            if (r == null)
                throw new InvalidOperationException($"Роль «{rn}» не найдена в базе.");
            roleIds[rn] = r.Id;
        }

        var incoming = new Dictionary<(string Role, string Item), bool>();
        foreach (var e in entries)
        {
            var role = e.Role?.Trim() ?? "";
            var item = e.Item?.Trim() ?? "";
            if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(item))
                continue;
            if (!roleIds.ContainsKey(role) || !permissionByName.ContainsKey(item))
                continue;
            incoming[(role, item)] = e.Granted;
        }

        var grantedByRole = MatrixRoleNames.ToDictionary(
            rn => rn,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var ((role, item), granted) in incoming)
        {
            if (granted)
                grantedByRole[role].Add(item);
        }

        foreach (var rn in MatrixRoleNames)
        {
            if (grantedByRole[rn].Count == 0)
                throw new InvalidOperationException($"У роли «{rn}» должно остаться хотя бы одно разрешение.");
        }

        foreach (var rid in roleIds.Values.Distinct())
        {
            await _db.RolePermissions.Where(rp => rp.RoleId == rid)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var rn in MatrixRoleNames)
        {
            var rid = roleIds[rn];
            foreach (var permName in grantedByRole[rn])
            {
                var pid = permissionByName[permName];
                _db.RolePermissions.Add(new RolePermission { RoleId = rid, PermissionId = pid });
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<HashSet<string>> GetPermissionNamesForRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        await SyncPermissionsAndSeedDefaultsAsync(cancellationToken).ConfigureAwait(false);

        var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
        if (role == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var names = await _db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Join(_db.Permissions.AsNoTracking(), rp => rp.PermissionId, p => p.PermissionId, (_, p) => p.PermissionName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return names.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public DashboardUiVisibility BuildDashboardVisibility(IReadOnlySet<string> permissionNames)
    {
        var visibility = new DashboardVisibilityBuilder();
        foreach (var item in _permissionsCatalog.GetPermissions())
        {
            if (!permissionNames.Contains(item.Id))
                continue;
            if (MonitoringPermissionDefaults.TryResolveUiBlock(item.Id, out var block))
                visibility.Grant(block);
        }

        return visibility.Build();
    }

    private sealed class DashboardVisibilityBuilder
    {
        private bool _waitingCard;
        private bool _inServiceCard;
        private bool _acceptedTodayCard;
        private bool _ticketsIssuedCard;
        private bool _queueTable;
        private bool _doctorLoad;

        public void Grant(DashboardUiBlock block)
        {
            switch (block)
            {
                case DashboardUiBlock.WaitingCard: _waitingCard = true; break;
                case DashboardUiBlock.InServiceCard: _inServiceCard = true; break;
                case DashboardUiBlock.AcceptedTodayCard: _acceptedTodayCard = true; break;
                case DashboardUiBlock.TicketsIssuedCard: _ticketsIssuedCard = true; break;
                case DashboardUiBlock.QueueTable: _queueTable = true; break;
                case DashboardUiBlock.DoctorLoad: _doctorLoad = true; break;
            }
        }

        public DashboardUiVisibility Build() => new()
        {
            WaitingCard = _waitingCard,
            InServiceCard = _inServiceCard,
            AcceptedTodayCard = _acceptedTodayCard,
            TicketsIssuedCard = _ticketsIssuedCard,
            QueueTable = _queueTable,
            DoctorLoad = _doctorLoad,
        };
    }
}
