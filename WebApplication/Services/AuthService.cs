using Microsoft.AspNetCore.Identity;
using MyWebApplication.Dto;
using MyWebApplication.Services;
using WebApplication.Models;

namespace WebApplication.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IPasswordGeneratorService _passwordGeneratorService;

        public AuthService(UserManager<User> userManager, ITokenService tokenService, IPasswordGeneratorService passwordGeneratorService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _passwordGeneratorService = passwordGeneratorService;
        }

        public async Task<ServiceResult<TokenResponseDto>> LoginAsync(LoginRequestDto request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return ServiceResult<TokenResponseDto>.Fail(new[] { "Пользователь не найден" });
            }
            if (!await _userManager.CheckPasswordAsync(user, request.Password))
            {
                return ServiceResult<TokenResponseDto>.Fail(new[] { "Неверный пароль" });
            }
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                return ServiceResult<TokenResponseDto>.Fail(new[] { "Email не подтвержден" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var tokenResponse = await _tokenService.CreateTokenResponseAsync(user, roles);

            user.RefreshToken = tokenResponse.RefreshToken;
            user.RefreshTokenExpireTime = DateTime.UtcNow.AddDays(7);
            await _userManager.UpdateAsync(user);

            return ServiceResult<TokenResponseDto>.Success(tokenResponse);
        }

        public async Task<ServiceResult<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request) 
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user is null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpireTime <= DateTime.UtcNow)
            {
                return ServiceResult<TokenResponseDto>.Fail(new[] { "Неверный или просроченный refresh token" });
            }
            var roles = await _userManager.GetRolesAsync(user);
            var tokenResponse = await _tokenService.CreateTokenResponseAsync(user, roles);
            return ServiceResult<TokenResponseDto>.Success(tokenResponse);
        }

        public async Task<ServiceResult> ConfirmEmailAsync(Guid userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
                return ServiceResult.Fail(new[] { "Пользователь не найден" });

            var decodedToken = Uri.UnescapeDataString(token);

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (!result.Succeeded)
                return ServiceResult.Fail(result.Errors.Select(e => e.Description));

            return ServiceResult.Success("Email успешно подтвержден");
        }

        public async Task<ServiceResult<PasswordResetDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
                return ServiceResult<PasswordResetDto>.Fail(new[] { "Пользователь не найден или email не подтвержден" });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            return ServiceResult<PasswordResetDto>.Success(new PasswordResetDto
            {
                UserId = user.Id,
                Token = token
            });
        }

        public async Task<ServiceResult> ResetPasswordAsync(PasswordResetTokenRequestDto request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return ServiceResult.Fail(new[] { "Пользователь не найден" });

            var decodedToken = Uri.UnescapeDataString(request.PasswordResetToken);

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
            if (!result.Succeeded)
                return ServiceResult.Fail(result.Errors.Select(e => e.Description));

            return ServiceResult.Success("Пароль успешно сброшен");
        }
    }
}
