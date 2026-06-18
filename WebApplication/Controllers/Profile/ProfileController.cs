using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Models.Emails;
using WebApplication.Services;
using WebApplication.Services.Auth;
using WebApplication.Services.Emails;

namespace WebApplication.Controllers.Profile {
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserProfileService _profileService;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateRenderer _emailTemplates;

        public ProfileController(
            IUserProfileService profileService,
            IEmailService emailService,
            IEmailTemplateRenderer emailTemplates)
        {
            _profileService = profileService;
            _emailService = emailService;
            _emailTemplates = emailTemplates;
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
            if (string.IsNullOrWhiteSpace(model.Password?.CurrentPassword) &&
                !string.IsNullOrWhiteSpace(model.Password?.NewPassword))
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

            var emailChange = result.Data?.EmailChange;
            if (emailChange != null)
            {
                var confirmationLink = Url.Action(
                    action: "ConfirmChangeEmail",
                    controller: "Account",
                    values: new { userId = emailChange.UserId, email = emailChange.NewEmail, token = emailChange.Token },
                    protocol: Request.Scheme);

                if (string.IsNullOrEmpty(confirmationLink))
                {
                    ModelState.AddModelError(string.Empty, "Не удалось сформировать ссылку подтверждения смены email");
                    return View(model);
                }

                var body = await _emailTemplates.RenderChangeEmailAsync(new ChangeEmailEmailViewModel
                {
                    CurrentEmail = emailChange.CurrentEmail,
                    NewEmail = emailChange.NewEmail,
                    ConfirmationLink = confirmationLink
                });

                await _emailService.SendEmailAsync(emailChange.NewEmail, "Подтверждение смены почты", body);
                TempData["ProfileSuccess"] = "Данные сохранены. На новый email отправлена ссылка подтверждения.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ProfileSuccess"] = "Данные успешно сохранены";
            return RedirectToAction(nameof(Index));
        }
    }
}
