using Microsoft.AspNetCore.Identity;
using WebApplication.Dto;
using WebApplication.Models;

namespace WebApplication.Services
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

        public async Task<ServiceResult<UserDto>> UpdateAsync(string userId, UserDto request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult<UserDto>.Fail(new[] { "Пользователь не найден", userId });

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                user.LastName = request.LastName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                user.FirstName = request.FirstName.Trim();
            }

            user.Patronymic = request.Patronymic?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
            {
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null && existingUser.Id != user.Id)
                    return ServiceResult<UserDto>.Fail(new[] { "Email уже используется" });

                user.Email = request.Email;
                user.UserName = request.Email;
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
                    return ServiceResult<UserDto>.Fail(pwdResult.Errors.Select(e => e.Description));
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return ServiceResult<UserDto>.Fail(updateResult.Errors.Select(e => e.Description));

            return ServiceResult<UserDto>.Success(new UserDto
            {
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Patronymic = user.Patronymic,
                Email = user.Email,
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Dispatcher"
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
