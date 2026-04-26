using Microsoft.IdentityModel.Tokens;
using MyWebApplication.Dto;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebApplication.Models;

namespace MyWebApplication.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<TokenResponseDto> CreateTokenResponseAsync(User user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            if (roles.Any())
                claims.Add(new Claim(ClaimTypes.Role, roles.First()));

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["AppSettings:Token"]!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

            var tokenDescriptor = new JwtSecurityToken(
                issuer: _configuration["AppSettings:Issuer"],
                audience: _configuration["AppSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );

            return new TokenResponseDto
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor),
                RefreshToken = GenerateRefreshToken()
            };
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}
