using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebApplication.Configuration;

namespace WebApplication.Services.Auth {
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly JwtOptions _jwtOptions;

        public TokenService(IConfiguration configuration, IOptions<JwtOptions> jwtOptions)
        {
            _configuration = configuration;
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<TokenResponseDto> CreateTokenResponseAsync(User user, IList<string> roles)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
            var primaryRole = roles.FirstOrDefault() ?? string.Empty;
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            if (roles.Any())
                claims.Add(new Claim(ClaimTypes.Role, primaryRole));

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(JwtConfiguration.GetRequiredSigningKey(_configuration)));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

            var tokenDescriptor = new JwtSecurityToken(
                issuer: _configuration["AppSettings:Issuer"],
                audience: _configuration["AppSettings:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds
            );

            return new TokenResponseDto
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor),
                RefreshToken = GenerateRefreshToken(),
                Expires = expiresAt,
                Role = primaryRole
            };
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return WebEncoders.Base64UrlEncode(randomNumber);
        }
    }
}
