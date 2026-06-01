using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Services;
using WebApplication.Services.Common.Authorization;

namespace WebApplication.ViewComponents.Shared;

public class SidebarViewComponent : ViewComponent
{
    private readonly UserManager<User> _userManager;
    private readonly IUserPermissionContext _permissionContext;
    private readonly IReportsCatalog _reportsCatalog;

    public SidebarViewComponent(
        UserManager<User> userManager,
        IUserPermissionContext permissionContext,
        IReportsCatalog reportsCatalog)
    {
        _userManager = userManager;
        _permissionContext = permissionContext;
        _reportsCatalog = reportsCatalog;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var controller = RouteData.Values["controller"]?.ToString() ?? "";
        var action = RouteData.Values["action"]?.ToString() ?? "";

        var user = await _userManager.GetUserAsync(HttpContext.User);

        string email = user?.Email ?? "";
        string fullName = BuildFullName(user);

        var menuItems = new List<SidebarItem>
        {
            new("Dashboard/Index", "Мониторинг", "/dashboard"),
        };

        if (user != null)
        {
            var permissions = await _permissionContext.GetCurrentPermissionNamesAsync();
            var reportIds = _reportsCatalog.GetCatalog().Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var canReports = permissions.Any(p => reportIds.Contains(p));

            if (canReports)
                menuItems.Add(new SidebarItem("Reports/Index", "Отчёты", "/reports"));

            if (User.IsInRole("Admin"))
                menuItems.Add(new SidebarItem("Users/Index", "Управление", "/users"));
        }

        var model = new SidebarViewModel
        {
            ActiveKey = $"{controller}/{action}".ToLowerInvariant(),
            MenuItems = menuItems,
            UserEmail = email,
            UserFullName = fullName
        };

        return View(model);
    }

    private static string GetInitial(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : char.ToUpper(value.Trim()[0]).ToString();
    }

    private string BuildFullName(User? user)
    {
        if (user == null) return "";

        var initials = $"{GetInitial(user.FirstName)}.{GetInitial(user.Patronymic)}.";

        return $"{user.LastName} {initials}";
    }
}
