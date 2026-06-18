
namespace WebApplication.Services.Users {
    public interface IUserService
    {
        Task<ServiceResult<RegistrationResultDto>> RegisterAsync(RegisterRequestDto request);
        Task<ServiceResult<UserUpdateResultDto>> UpdateAsync(string userId, UserDto request);
        Task<ServiceResult> DeleteAsync(string userId);
    }
}
