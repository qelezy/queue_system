namespace WebApplication.Models.Identity;

public class RolePermission
{
    public string RoleId { get; set; } = "";

    public int PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
