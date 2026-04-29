using WebApplication.Dto;
using WebApplication.Models;

namespace WebApplication.Services
{
    public interface ITokenService
    {
        Task<TokenResponseDto> CreateTokenResponseAsync(User user, IList<string> roles);
    }
}
