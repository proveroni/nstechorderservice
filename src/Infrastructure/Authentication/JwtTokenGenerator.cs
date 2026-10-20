using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderService.Application.Abstractions.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace OrderService.Infrastructure.Authentication
{
    /// <summary>
    /// Implementa a geração de tokens de autenticação JWT.
    /// </summary>
    public sealed class JwtTokenGenerator : ITokenGenerator
    {
        private readonly JwtOptions _options;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Inicializa o gerador de tokens JWT.
        /// </summary>
        /// <param name="options">
        /// Configurações do JWT.
        /// </param>
        /// <param name="timeProvider">
        /// Provedor de data e hora.
        /// </param>
        public JwtTokenGenerator
            (
                IOptions<JwtOptions> options,
                TimeProvider timeProvider
            )
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(timeProvider);

            _options = options.Value;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Gera um token JWT assinado para o usuário informado,
        /// utilizando as configurações de autenticação da aplicação.
        /// </summary>
        /// <param name="userId">
        /// Identificador do usuário que será incluído na claim de assunto do token.
        /// </param>
        /// <returns>
        /// Token de acesso JWT e seu tempo de expiração, em segundos.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Lançada quando o identificador do usuário é nulo, vazio ou contém apenas espaços.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Lançada quando o tempo de expiração configurado não é maior que zero.
        /// </exception>
        public GeneratedToken Generate(string userId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            var issuer = _options.Issuer;
            var audience = _options.Audience;
            var signingKey = _options.SigningKey;
            var expirationMinutes = _options.ExpirationMinutes;

            if (expirationMinutes <= 0)
            {
                throw new InvalidOperationException("Jwt:ExpirationMinutes deve ser maior que zero.");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expires = now.AddMinutes(expirationMinutes);

            var keyBytes = Convert.FromBase64String(signingKey);

            var signingCredentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString("N"))
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: signingCredentials);

            return new GeneratedToken
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresIn = checked(expirationMinutes * 60)
            };
        }
    }
}
