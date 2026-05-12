namespace WebApplication.Models;

public class RolePermission
{
    public string RoleId { get; set; } = "";

    public int PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
