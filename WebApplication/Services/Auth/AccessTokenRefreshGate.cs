using System.IdentityModel.Tokens.Jwt;

namespace WebApplication.Services.Auth {
    
    public static class AccessTokenRefreshGate
    {
        public static bool ShouldTryRefresh(string? accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return true;

            try
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
                return jwt.ValidTo <= DateTime.UtcNow;
            }
            catch
            {
                return true;
            }
        }
    }
}
