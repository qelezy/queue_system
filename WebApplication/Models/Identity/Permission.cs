namespace WebApplication.Models.Identity;

public class Permission
{
    public int PermissionId { get; set; }

    /// <summary>Совпадает с id отчёта в каталоге или с ключом блока дашборда (например dashboard.waiting).</summary>
    public string PermissionName { get; set; } = "";

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
