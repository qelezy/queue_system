using MyWebApplication.Dto;

namespace MyWebApplication.Services
{
    public interface IAuthService
    {
        Task<ServiceResult<TokenResponseDto>> LoginAsync(LoginRequestDto request);
        Task<ServiceResult<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task<ServiceResult> ConfirmEmailAsync(Guid userId, string token);
        Task<ServiceResult<PasswordResetDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request);
        Task<ServiceResult> ResetPasswordAsync(PasswordResetTokenRequestDto request);
    }
}
