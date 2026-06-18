using Microsoft.AspNetCore.Identity;

namespace WebApplication.Services.Users
{
    public class UserService : IUserService
    {
        private readonly UserManager<User> _userManager;
        private readonly IPasswordGeneratorService _passwordGeneratorService;

        public UserService(UserManager<User> userManager, IPasswordGeneratorService passwordGeneratorService)
        {
            _userManager = userManager;
            _passwordGeneratorService = passwordGeneratorService;
        }

        public async Task<ServiceResult<RegistrationResultDto>> RegisterAsync(RegisterRequestDto request)
        {
            if (await _userManager.FindByEmailAsync(request.Email) != null)
            {
                return ServiceResult<RegistrationResultDto>.Fail(new[] { "Email уже занят" });
            }

            var user = new User { FirstName = request.FirstName, LastName = request.LastName, Patronymic = request.Patronymic, Email = request.Email, UserName = request.Email };

            var generatedPassword = _passwordGeneratorService.GeneratePassword();

            var result = await _userManager.CreateAsync(user, generatedPassword);
            if (!result.Succeeded)
            {
                return ServiceResult<RegistrationResultDto>.Fail(result.Errors.Select(e => e.Description));
            }

            var normalizedRole = (request.Role ?? string.Empty).Trim();
            var roleToAssign = string.IsNullOrWhiteSpace(normalizedRole) ? "Dispatcher" : normalizedRole;

            await _userManager.AddToRoleAsync(user, roleToAssign);

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            return ServiceResult<RegistrationResultDto>.Success(new RegistrationResultDto
            {
                UserId = user.Id,
                Email = user.Email,
                Password = generatedPassword,
                Token = token
            });
        }

        public async Task<ServiceResult<UserUpdateResultDto>> UpdateAsync(string userId, UserDto request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult<UserUpdateResultDto>.Fail(new[] { "Пользователь не найден", userId });

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                user.LastName = request.LastName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                user.FirstName = request.FirstName.Trim();
            }

            user.Patronymic = request.Patronymic?.Trim() ?? string.Empty;

            EmailChangeConfirmationDto? emailChange = null;
            var requestedEmail = request.Email?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(requestedEmail)
                && !string.Equals(requestedEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await _userManager.FindByEmailAsync(requestedEmail);
                if (existingUser != null && existingUser.Id != user.Id)
                    return ServiceResult<UserUpdateResultDto>.Fail(new[] { "Email уже используется" });

                var token = await _userManager.GenerateChangeEmailTokenAsync(user, requestedEmail);
                emailChange = new EmailChangeConfirmationDto
                {
                    UserId = user.Id,
                    CurrentEmail = user.Email ?? string.Empty,
                    NewEmail = requestedEmail,
                    Token = token
                };
            }

            if (!string.IsNullOrEmpty(request.Role))
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!currentRoles.Contains(request.Role))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, request.Role);
                }
            }

            if (!string.IsNullOrEmpty(request.Password))
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwdResult = await _userManager.ResetPasswordAsync(user, resetToken, request.Password);
                if (!pwdResult.Succeeded)
                    return ServiceResult<UserUpdateResultDto>.Fail(pwdResult.Errors.Select(e => e.Description));
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return ServiceResult<UserUpdateResultDto>.Fail(updateResult.Errors.Select(e => e.Description));

            return ServiceResult<UserUpdateResultDto>.Success(new UserUpdateResultDto
            {
                User = new UserDto
                {
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Patronymic = user.Patronymic,
                    Email = user.Email ?? string.Empty,
                    Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Dispatcher"
                },
                EmailChange = emailChange
            });
        }

        public async Task<ServiceResult> DeleteAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult.Fail(new[] { "Пользователь не найден" });

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return ServiceResult.Fail(result.Errors.Select(e => e.Description));

            return ServiceResult.Success("Пользователь успешно удален");
        }
    }
}
