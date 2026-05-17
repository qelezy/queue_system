using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using WebApplication.Dto;
using WebApplication.Models;

namespace WebApplication.Services;

public sealed class RolePermissionService : IRolePermissionService
{
    private static readonly string[] MatrixRoleNames = ["Admin", "Manager", "Dispatcher"];

    private static readonly Dictionary<string, string> LegacyReportPermissionIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["service-delays"] = "bottleneck-ranking",
        ["no-shows-and-incomplete-service"] = "unserved-and-chain-breaks",
    };

    private static readonly (string Name, string Title, string Description)[] DashboardLines =
    [
        ("dashboard.waiting", "Ожидают", "Карточка: записи в очереди без вызова к врачу, приём ещё не завершён."),
        ("dashboard.in-service", "На приёме", "Карточка: пациенты с начатым и ещё не завершённым приёмом."),
        ("dashboard.accepted-today", "Обслужено", "Карточка: завершённые приёмы за сегодня."),
        ("dashboard.noshow-today", "Не явились", "Карточка: неявки за сегодня."),
        ("dashboard.avg-wait", "Среднее время ожидания", "Карточка: среднее и максимальное время ожидания (мин.) по завершённым ожиданиям за сегодня."),
        ("dashboard.avg-service", "Средняя длительность приёма", "Карточка: средняя и максимальная длительность приёма (мин.) за сегодня."),
        ("dashboard.chart-cabinets-load", "Состояние врачей", "Таблица: врач, статус, длительность текущего приёма, норма, число записей в очереди."),
        ("dashboard.queue-table", "Текущая очередь", "Таблица: пациент, врач, кабинет, время записи, ожидание (мин.), статус."),
    ];

    private readonly AppDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IReportsCatalog _reportsCatalog;

    public RolePermissionService(
        AppDbContext db,
        RoleManager<IdentityRole> roleManager,
        IReportsCatalog reportsCatalog)
    {
        _db = db;
        _roleManager = roleManager;
        _reportsCatalog = reportsCatalog;
    }

    public async Task SyncPermissionsAndSeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionRowsAsync(cancellationToken).ConfigureAwait(false);
        await MigrateLegacyReportPermissionsAsync(cancellationToken).ConfigureAwait(false);
        await RemoveOrphanReportPermissionsAsync(cancellationToken).ConfigureAwait(false);
        await SeedDefaultRoleLinksIfEmptyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsurePermissionRowsAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Permissions
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, _, _) in DashboardLines)
        {
            if (set.Add(name))
                _db.Permissions.Add(new Permission { PermissionName = name });
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

    private async Task MigrateLegacyReportPermissionsAsync(CancellationToken cancellationToken)
    {
        foreach (var (currentId, legacyId) in LegacyReportPermissionIds)
        {
            var permissions = await _db.Permissions
                .Where(p => p.PermissionName == currentId || p.PermissionName == legacyId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var current = permissions.FirstOrDefault(p =>
                string.Equals(p.PermissionName, currentId, StringComparison.OrdinalIgnoreCase));
            var legacy = permissions.FirstOrDefault(p =>
                string.Equals(p.PermissionName, legacyId, StringComparison.OrdinalIgnoreCase));

            if (legacy is null)
                continue;

            if (current is null)
            {
                legacy.PermissionName = currentId;
                continue;
            }

            if (legacy.PermissionId == current.PermissionId)
                continue;

            var legacyRoleIds = await _db.RolePermissions.AsNoTracking()
                .Where(rp => rp.PermissionId == legacy.PermissionId)
                .Select(rp => rp.RoleId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var currentRoleIds = await _db.RolePermissions.AsNoTracking()
                .Where(rp => rp.PermissionId == current.PermissionId)
                .Select(rp => rp.RoleId)
                .ToHashSetAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var roleId in legacyRoleIds)
            {
                if (currentRoleIds.Contains(roleId))
                    continue;

                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = current.PermissionId,
                });
            }

            await _db.RolePermissions
                .Where(rp => rp.PermissionId == legacy.PermissionId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _db.Permissions.Remove(legacy);
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveOrphanReportPermissionsAsync(CancellationToken cancellationToken)
    {
        var knownReportIds = _reportsCatalog.GetCatalog()
            .Select(i => i.Id.Trim())
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var legacyId in LegacyReportPermissionIds.Values)
            knownReportIds.Add(legacyId);

        var orphans = await _db.Permissions
            .Where(p => !p.PermissionName.StartsWith("dashboard."))
            .Where(p => !knownReportIds.Contains(p.PermissionName))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var orphan in orphans)
        {
            await _db.RolePermissions
                .Where(rp => rp.PermissionId == orphan.PermissionId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            _db.Permissions.Remove(orphan);
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool DefaultGrant(string roleName, string permissionName)
    {
        if (permissionName.StartsWith("dashboard.manager.", StringComparison.OrdinalIgnoreCase))
            return !string.Equals(roleName, "Dispatcher", StringComparison.OrdinalIgnoreCase);
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
        foreach (var (name, title, desc) in DashboardLines)
        {
            if (!permByName.TryGetValue(name, out var pid))
                continue;

            vizItems.Add(new AccessItemViewModel
            {
                Key = name,
                Title = title,
                Description = desc,
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
                Title = "Визуализации мониторинга очереди",
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

        var set = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ExpandLegacyReportPermissionNames(set);
        return set;
    }

    private static void ExpandLegacyReportPermissionNames(HashSet<string> permissionNames)
    {
        foreach (var (currentId, legacyId) in LegacyReportPermissionIds)
        {
            if (permissionNames.Contains(legacyId))
                permissionNames.Add(currentId);
        }
    }

    public DashboardUiVisibility BuildDashboardVisibility(IReadOnlySet<string> permissionNames)
    {
        bool Has(string n) => permissionNames.Contains(n);

        return new DashboardUiVisibility
        {
            WaitingCard = Has("dashboard.waiting"),
            InServiceCard = Has("dashboard.in-service"),
            AcceptedTodayCard = Has("dashboard.accepted-today"),
            NoShowTodayCard = Has("dashboard.noshow-today"),
            AvgWaitCard = Has("dashboard.avg-wait"),
            AvgServiceCard = Has("dashboard.avg-service"),
            QueueTable = Has("dashboard.queue-table"),
            DoctorLoad = Has("dashboard.chart-cabinets-load"),
        };
    }
}
