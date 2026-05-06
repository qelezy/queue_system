namespace WebApplication.Models;

public class AccessSettingsViewModel
{
    public IReadOnlyList<AccessRoleColumn> Roles { get; init; } = [];
    public IReadOnlyList<AccessGroupViewModel> Groups { get; init; } = [];
    public string SaveOnClick { get; init; } = "AccessSettingsUI.save()";
    public string SaveButtonText { get; init; } = "Сохранить изменения";
}

public record AccessRoleColumn(string Key, string Label);

public class AccessGroupViewModel
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Icon { get; init; } = "";
    public IReadOnlyList<AccessItemViewModel> Items { get; init; } = [];
}

public class AccessItemViewModel
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public IDictionary<string, bool> RolePermissions { get; init; } = new Dictionary<string, bool>();
}
