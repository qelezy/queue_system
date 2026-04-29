using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;

        public UsersController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Управление пользователями";

            var dbUsers = _userManager.Users.ToList();
            var users = new List<UserRowViewModel>(dbUsers.Count);

            foreach (var user in dbUsers)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Registrator";
                var firstName = user.FirstName ?? string.Empty;
                var lastName = user.LastName ?? string.Empty;
                var patronymic = user.Patronymic ?? string.Empty;
                var fullName = string.Join(" ", new[] { lastName, firstName, patronymic }.Where(x => !string.IsNullOrWhiteSpace(x)));

                users.Add(new UserRowViewModel
                {
                    Id = user.Id,
                    LastName = lastName,
                    FirstName = firstName,
                    Patronymic = patronymic,
                    FullName = fullName,
                    Email = user.Email ?? string.Empty,
                    Role = role,
                    RoleName = role switch
                    {
                        "Admin" => "Администратор",
                        "Manager" => "Менеджер",
                        _ => "Регистратор"
                    }
                });
            }

            var model = new UsersPageViewModel
            {
                Users = users,
                RegisterUser = new RegisterUserViewModel()
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterUserViewModel());
        }
    }
}