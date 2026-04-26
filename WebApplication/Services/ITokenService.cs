using MyWebApplication.Dto;
using WebApplication.Models;

namespace MyWebApplication.Services
{
    public interface ITokenService
    {
        Task<TokenResponseDto> CreateTokenResponseAsync(User user, IList<string> roles);
    }
}
