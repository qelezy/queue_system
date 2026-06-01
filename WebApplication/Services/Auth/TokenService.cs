using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebApplication.Configuration;
using WebApplication.Models.Configuration;

namespace WebApplication.Services.Auth;

public class TokenService : ITokenService
{
    private readonly AppSettingsOptions _appSettings;
    private readonly JwtOptions _jwtOptions;

    public TokenService(IOptions<AppSettingsOptions> appSettings, IOptions<JwtOptions> jwtOptions)
    {
        _appSettings = appSettings.Value;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<TokenResponseDto> CreateTokenResponseAsync(User user, IList<string> roles)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var primaryRole = roles.FirstOrDefault() ?? string.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        if (roles.Any())
            claims.Add(new Claim(ClaimTypes.Role, primaryRole));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(JwtConfiguration.GetRequiredSigningKey(_appSettings)));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _appSettings.Issuer,
            audience: _appSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

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
