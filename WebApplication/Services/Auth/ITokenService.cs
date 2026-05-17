
namespace WebApplication.Services.Auth {
    public interface ITokenService
    {
        Task<TokenResponseDto> CreateTokenResponseAsync(User user, IList<string> roles);
    }
}
