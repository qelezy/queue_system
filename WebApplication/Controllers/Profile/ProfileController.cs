using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Services;

namespace WebApplication.Controllers.Profile {
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserProfileService _profileService;

        public ProfileController(IUserProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var result = await _profileService.GetProfileAsync(User);

            if (!result.Succeeded)
                return BadRequest(result.Errors);
            if (result.Data == null)
                return BadRequest(new[] { "Данные профиля не найдены" });

            var profileModel = new UserProfileViewModel
            {
                FirstName = result.Data.FirstName,
                LastName = result.Data.LastName,
                Patronymic = result.Data.Patronymic,
                Email = result.Data.Email
            };

            var model = new UserProfilePageViewModel
            {
                Profile = profileModel,
                Password = new ChangePasswordViewModel()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(UserProfilePageViewModel model)
        {
            if ((string.IsNullOrWhiteSpace(model.Password?.CurrentPassword) &&
                 !string.IsNullOrWhiteSpace(model.Password?.NewPassword)) ||
                (!string.IsNullOrWhiteSpace(model.Password?.CurrentPassword) &&
                 string.IsNullOrWhiteSpace(model.Password?.NewPassword)))
            {
                ModelState.AddModelError(string.Empty, "Для смены пароля заполните оба поля");
            }

            if (!string.IsNullOrWhiteSpace(model.Password?.NewPassword) &&
                model.Password.NewPassword.Length < 6)
            {
                ModelState.AddModelError(string.Empty, "Новый пароль должен быть не короче 6 символов");
            }

            if (!ModelState.IsValid)
                return View(model);

            var result = await _profileService.UpdateProfileAsync(User, model);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors ?? Array.Empty<string>())
                {
                    if (error == "PASSWORD_CURRENT_INVALID" || error.Contains("Текущий пароль", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(string.Empty, "Текущий пароль указан неверно");
                        continue;
                    }

                    if (error.Contains("Новый пароль", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(string.Empty, error);
                        continue;
                    }

                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
            }

            TempData["ProfileSuccess"] = "Данные успешно сохранены";
            return RedirectToAction(nameof(Index));
        }
    }
}
