using System.Security.Claims;
using WebApplication.Dto;
using WebApplication.Models;

namespace WebApplication.Services
{
    public interface IUserProfileService
    {
        Task<ServiceResult<UserProfileDto>> GetProfileAsync(ClaimsPrincipal user);
        Task<ServiceResult> UpdateProfileAsync(ClaimsPrincipal user, UserProfilePageViewModel model);
    }
}
