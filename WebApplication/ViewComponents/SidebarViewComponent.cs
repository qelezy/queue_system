using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApplication.Models;

namespace WebApplication.ViewComponents;

public class SidebarViewComponent : ViewComponent
{
    private readonly UserManager<User> _userManager;

    public SidebarViewComponent(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var controller = RouteData.Values["controller"]?.ToString() ?? "";
        var action = RouteData.Values["action"]?.ToString() ?? "";

        var user = await _userManager.GetUserAsync(HttpContext.User);

        string email = user?.Email ?? "";
        string fullName = BuildFullName(user);

        var model = new SidebarViewModel
        {
            ActiveKey = $"{controller}/{action}".ToLowerInvariant(),

            MenuItems =
            [
                new SidebarItem("Dashboard/Index", "Мониторинг очереди", "/dashboard"),
                new SidebarItem("Reports/Index", "Отчёты", "/reports"),
                new SidebarItem("Users/Index", "Управление пользователями", "/users"),
            ],

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