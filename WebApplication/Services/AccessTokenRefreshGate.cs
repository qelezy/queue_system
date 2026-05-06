using System.IdentityModel.Tokens.Jwt;

namespace WebApplication.Services
{
    /// <summary>
    /// Дешёвая проверка без валидации подписи: только парсинг и срок, чтобы не дублировать полный JwtBearer.Validate на каждом запросе.
    /// Неверная подпись обрабатывается JwtBearer (одна криптопроверка на запрос).
    /// </summary>
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
