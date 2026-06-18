using System.Security.Claims;

namespace WebApplication.Services.Auth {
    public interface IAuthService
    {
        Task<ServiceResult<TokenResponseDto>> LoginAsync(LoginRequestDto request);
        Task<ServiceResult<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task<ServiceResult<TokenResponseDto>> RefreshTokenByTokenAsync(string refreshToken);
        Task<ServiceResult> LogoutAsync(ClaimsPrincipal? principal, string? refreshTokenFromCookie = null);
        Task<ServiceResult> ConfirmEmailAsync(Guid userId, string token);
        Task<ServiceResult> ConfirmChangeEmailAsync(Guid userId, string email, string token);
        Task<ServiceResult<PasswordResetDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request);
        Task<ServiceResult> ResetPasswordAsync(PasswordResetTokenRequestDto request);
    }
}
