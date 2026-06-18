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

        public async Task<ServiceResult<UserProfileUpdateResultDto>> UpdateProfileAsync(ClaimsPrincipal userPrincipal, UserProfilePageViewModel model)
        {
            var user = await _userManager.GetUserAsync(userPrincipal);

            if (user == null)
                return ServiceResult<UserProfileUpdateResultDto>.Fail(new[] { "Пользователь не найден" });

            var requestedEmail = model.Profile.Email?.Trim() ?? string.Empty;
            var emailChanged = !string.Equals(user.Email, requestedEmail, StringComparison.OrdinalIgnoreCase);

            var hasPasswordChangeRequest =
                !string.IsNullOrWhiteSpace(model.Password?.CurrentPassword) ||
                !string.IsNullOrWhiteSpace(model.Password?.NewPassword);

            if (emailChanged || hasPasswordChangeRequest)
            {
                var currentPassword = model.Password?.CurrentPassword ?? string.Empty;
                var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, currentPassword);
                if (!isCurrentPasswordValid)
                {
                    return ServiceResult<UserProfileUpdateResultDto>.Fail(new[] { "PASSWORD_CURRENT_INVALID" });
                }
            }

            EmailChangeConfirmationDto? emailChange = null;
            if (emailChanged)
            {
                if (string.IsNullOrWhiteSpace(requestedEmail))
                    return ServiceResult<UserProfileUpdateResultDto>.Fail(new[] { "Email не задан" });

                var existingUser = await _userManager.FindByEmailAsync(requestedEmail);
                if (existingUser != null && existingUser.Id != user.Id)
                    return ServiceResult<UserProfileUpdateResultDto>.Fail(new[] { "Email уже используется" });

                var token = await _userManager.GenerateChangeEmailTokenAsync(user, requestedEmail);
                emailChange = new EmailChangeConfirmationDto
                {
                    UserId = user.Id,
                    CurrentEmail = user.Email ?? string.Empty,
                    NewEmail = requestedEmail,
                    Token = token
                };
            }

            user.FirstName = model.Profile.FirstName?.Trim() ?? string.Empty;
            user.LastName = model.Profile.LastName?.Trim() ?? string.Empty;
            user.Patronymic = model.Profile.Patronymic?.Trim();

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return ServiceResult<UserProfileUpdateResultDto>.Fail(result.Errors.Select(x => x.Description));

            if (!string.IsNullOrWhiteSpace(model.Password?.NewPassword))
            {
                var passResult = await _userManager.ChangePasswordAsync(
                    user,
                    model.Password?.CurrentPassword ?? string.Empty,
                    model.Password?.NewPassword ?? string.Empty);

                if (!passResult.Succeeded)
                {
                    var mappedErrors = passResult.Errors.Select(MapPasswordError).ToList();
                    return ServiceResult<UserProfileUpdateResultDto>.Fail(mappedErrors);
                }
            }

            return ServiceResult<UserProfileUpdateResultDto>.Success(new UserProfileUpdateResultDto
            {
                EmailChange = emailChange
            });
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
