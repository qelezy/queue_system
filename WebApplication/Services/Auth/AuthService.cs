using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplication.Services;

namespace WebApplication.Services.Auth {
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IPasswordGeneratorService _passwordGeneratorService;
        private readonly JwtOptions _jwtOptions;

        public AuthService(UserManager<User> userManager, ITokenService tokenService, IPasswordGeneratorService passwordGeneratorService, IOptions<JwtOptions> jwtOptions)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _passwordGeneratorService = passwordGeneratorService;
            _jwtOptions = jwtOptions.Value;
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

            user.RefreshSessionExtended = request.RememberMe;
            user.RefreshToken = tokenResponse.RefreshToken;
            user.RefreshTokenExpiresAt = CalculateRefreshTokenExpiry(user.RefreshSessionExtended);
            await _userManager.UpdateAsync(user);

            tokenResponse.RefreshSessionExtended = user.RefreshSessionExtended;
            return ServiceResult<TokenResponseDto>.Success(tokenResponse);
        }

        public async Task<ServiceResult<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request) 
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return ServiceResult<TokenResponseDto>.Fail(new[] { "Отсутствует refresh token" });

            return await RefreshTokenByTokenAsync(request.RefreshToken);
        }

        public async Task<ServiceResult<TokenResponseDto>> RefreshTokenByTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return ServiceResult<TokenResponseDto>.Fail(new[] { "Отсутствует refresh token" });

            var user = await _userManager.Users.FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);
            if (user is null || !user.RefreshTokenExpiresAt.HasValue || user.RefreshTokenExpiresAt.Value <= DateTime.UtcNow)
                return ServiceResult<TokenResponseDto>.Fail(new[] { "Неверный или просроченный refresh token" });

            var roles = await _userManager.GetRolesAsync(user);
            var tokenResponse = await _tokenService.CreateTokenResponseAsync(user, roles);
            user.RefreshToken = tokenResponse.RefreshToken;
            user.RefreshTokenExpiresAt = CalculateRefreshTokenExpiry(user.RefreshSessionExtended);
            await _userManager.UpdateAsync(user);

            tokenResponse.RefreshSessionExtended = user.RefreshSessionExtended;
            return ServiceResult<TokenResponseDto>.Success(tokenResponse);
        }

        public async Task<ServiceResult> LogoutAsync(ClaimsPrincipal? principal, string? refreshTokenFromCookie = null)
        {
            User? user = null;
            if (!string.IsNullOrWhiteSpace(refreshTokenFromCookie))
                user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshTokenFromCookie);

            if (user is null && principal?.Identity?.IsAuthenticated == true)
            {
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                    user = await _userManager.FindByIdAsync(userId);
            }

            if (user is null)
                return ServiceResult.Success();

            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
            user.RefreshSessionExtended = false;
            await _userManager.UpdateAsync(user);
            return ServiceResult.Success();
        }

        private DateTime CalculateRefreshTokenExpiry(bool refreshSessionExtended)
        {
            return refreshSessionExtended
                ? DateTime.UtcNow.AddDays(_jwtOptions.RefreshRememberDays)
                : DateTime.UtcNow.AddHours(_jwtOptions.RefreshSessionHours);
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
