using WebApplication.Dto;

namespace WebApplication.Services
{
    public interface IUserService
    {
        Task<ServiceResult<RegistrationResultDto>> RegisterAsync(RegisterRequestDto request);
        Task<ServiceResult<UserDto>> UpdateAsync(string userId, UserDto request);
        Task<ServiceResult> DeleteAsync(string userId);
    }
}
