using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace WebApplication.Services.Users {
    public class UserProfileService : IUserProfileService
    {
        private readonly UserManager<User> _userManager;

        public UserProfileService(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<ServiceResult<UserProfileDto>> GetProfileAsync(ClaimsPrincipal userPrincipal)
        {
            var userId = _userManager.GetUserId(userPrincipal);
            if (string.IsNullOrWhiteSpace(userId))
                return ServiceResult<UserProfileDto>.Fail(new[] { "Пользователь не найден" });

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return ServiceResult<UserProfileDto>.Fail(
                    new[] { "Пользователь не найден" });

            return ServiceResult<UserProfileDto>.Success(new UserProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Patronymic = user.Patronymic,
                Email = user.Email!
            });
        }

        public async Task<ServiceResult> UpdateProfileAsync(ClaimsPrincipal userPrincipal, UserProfilePageViewModel model)
        {
            var user = await _userManager.GetUserAsync(userPrincipal);

            if (user == null)
                return ServiceResult.Fail(new[] { "Пользователь не найден" });

            var hasPasswordChangeRequest =
                !string.IsNullOrWhiteSpace(model.Password?.CurrentPassword) ||
                !string.IsNullOrWhiteSpace(model.Password?.NewPassword);

            if (hasPasswordChangeRequest)
            {
                var currentPassword = model.Password?.CurrentPassword ?? string.Empty;
                var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, currentPassword);
                if (!isCurrentPasswordValid)
                {
                    return ServiceResult.Fail(new[] { "PASSWORD_CURRENT_INVALID" });
                }
            }

            user.FirstName = model.Profile.FirstName?.Trim() ?? string.Empty;
            user.LastName = model.Profile.LastName?.Trim() ?? string.Empty;
            user.Patronymic = model.Profile.Patronymic?.Trim();

            if (!string.Equals(user.Email, model.Profile.Email,
                StringComparison.OrdinalIgnoreCase))
            {
                var normalizedEmail = (model.Profile.Email ?? string.Empty).Trim().ToUpperInvariant();
                user.Email = model.Profile.Email;
                user.UserName = model.Profile.Email;
                user.NormalizedEmail = normalizedEmail;
                user.NormalizedUserName = normalizedEmail;
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return ServiceResult.Fail(result.Errors.Select(x => x.Description));

            if (hasPasswordChangeRequest)
            {
                var passResult = await _userManager.ChangePasswordAsync(
                    user,
                    model.Password?.CurrentPassword ?? string.Empty,
                    model.Password?.NewPassword ?? string.Empty);

                if (!passResult.Succeeded)
                {
                    var mappedErrors = passResult.Errors.Select(MapPasswordError).ToList();
                    return ServiceResult.Fail(mappedErrors);
                }
            }

            return ServiceResult.Success();
        }

        private static string MapPasswordError(IdentityError error)
        {
            return error.Code switch
            {
                "PasswordMismatch" => "Текущий пароль указан неверно",
                "PasswordTooShort" => "Новый пароль слишком короткий",
                "PasswordRequiresNonAlphanumeric" => "Новый пароль должен содержать спецсимвол",
                "PasswordRequiresDigit" => "Новый пароль должен содержать цифру",
                "PasswordRequiresLower" => "Новый пароль должен содержать строчную букву",
                "PasswordRequiresUpper" => "Новый пароль должен содержать заглавную букву",
                _ => error.Description
            };
        }
    }
}
