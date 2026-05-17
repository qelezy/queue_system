using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication.Controllers.Users
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly IRolePermissionService _rolePermissionService;

        public UsersController(UserManager<User> userManager, IRolePermissionService rolePermissionService)
        {
            _userManager = userManager;
            _rolePermissionService = rolePermissionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Управление пользователями";

            var dbUsers = _userManager.Users.ToList();
            var users = new List<UserRowViewModel>(dbUsers.Count);

            foreach (var user in dbUsers)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Dispatcher";
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
                        "Dispatcher" => "Диспетчер",
                        _ => role
                    }
                });
            }

            var accessSettings = await _rolePermissionService.GetAccessMatrixAsync().ConfigureAwait(false);
            accessSettings.SaveMatrixUrl = Url.Action(nameof(SaveAccessMatrix), "Users") ?? "";

            var model = new UsersPageViewModel
            {
                Users = users,
                RegisterUser = new RegisterUserViewModel(),
                AccessSettings = accessSettings,
            };

            ViewData["CurrentUserId"] = _userManager.GetUserId(User) ?? "";

            return View(model);
        }

        [HttpPost]
        [Consumes("application/json")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAccessMatrix([FromBody] AccessMatrixSaveRequestDto? request)
        {
            if (request?.Entries == null || request.Entries.Count == 0)
                return BadRequest(new { message = "Нет данных для сохранения." });

            try
            {
                await _rolePermissionService.SaveAccessMatrixAsync(request.Entries).ConfigureAwait(false);
                return Json(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterUserViewModel());
        }
    }
}
