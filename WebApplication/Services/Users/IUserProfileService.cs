using System.Security.Claims;

namespace WebApplication.Services.Users {
    public interface IUserProfileService
    {
        Task<ServiceResult<UserProfileDto>> GetProfileAsync(ClaimsPrincipal user);
        Task<ServiceResult> UpdateProfileAsync(ClaimsPrincipal user, UserProfilePageViewModel model);
    }
}
