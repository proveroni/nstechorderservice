using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrderService.Infrastructure.Authentication;

namespace OrderService.Api.Configurations
{
    /// <summary>
    /// Centraliza a configuração de autenticação e autorização da API.
    /// </summary>
    public static class ApiAuthenticationConfiguration
    {
        /// <summary>
        /// Registra a autenticação JWT Bearer e a autorização.
        /// </summary>
        /// <param name="services">Coleção de serviços.</param>
        /// <param name="configuration">Configurações da aplicação.</param>
        /// <returns>A coleção de serviços configurada.</returns>
        public static IServiceCollection AddApiAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services
                .AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .Validate(
                    options =>
                        !string.IsNullOrWhiteSpace(options.Issuer) &&
                        !string.IsNullOrWhiteSpace(options.Audience) &&
                        !string.IsNullOrWhiteSpace(options.SigningKey) &&
                        options.ExpirationMinutes > 0,
                    "As configurações JWT são inválidas ou estão incompletas.")
                .ValidateOnStart();

            var issuer = configuration["Jwt:Issuer"];
            var audience = configuration["Jwt:Audience"];
            var signingKey = configuration["Jwt:SigningKey"];

            if (string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience) ||
                string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    "As configurações Jwt:Issuer, Jwt:Audience e Jwt:SigningKey são obrigatórias.");
            }

            byte[] keyBytes;

            try
            {
                keyBytes = Convert.FromBase64String(signingKey);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    "A chave de assinatura JWT deve estar codificada em Base64.",
                    exception);
            }

            if (keyBytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "A chave de assinatura JWT deve possuir pelo menos 32 bytes.");
            }

            services
                .AddOptions<DemoUserOptions>()
                .Bind(configuration.GetSection(DemoUserOptions.SectionName))
                .Validate(
                    options =>
                        !string.IsNullOrWhiteSpace(options.UserId) &&
                        !string.IsNullOrWhiteSpace(options.Username) &&
                        !string.IsNullOrWhiteSpace(options.PasswordHash),
                    "As configurações DemoUser:UserId, DemoUser:Username e DemoUser:PasswordHash são obrigatórias.")
                .ValidateOnStart();

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,

                        ValidateAudience = true,
                        ValidAudience = audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        RequireSignedTokens = true,

                        ClockSkew = TimeSpan.Zero
                    };
                });

            services.AddAuthorization();

            return services;
        }
    }
}
