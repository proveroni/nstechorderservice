using OrderService.Application.Abstractions.Authentication;
using System.IdentityModel.Tokens.Jwt;

namespace OrderService.Api.Features.Auth.Services
{
    /// <summary>
    /// Disponibiliza informações do usuário autenticado
    /// a partir do contexto HTTP da requisição.
    /// </summary>
    public sealed class HttpCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Inicializa o serviço de identificação do usuário atual.
        /// </summary>
        public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            ArgumentNullException.ThrowIfNull(httpContextAccessor);

            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Indica se a requisição HTTP atual possui um usuário autenticado.
        /// </summary>
        /// <value>
        /// <see langword="true"/> quando a identidade do usuário está autenticada;
        /// caso contrário, <see langword="false"/>.
        /// </value>
        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?
                .User.Identity?.IsAuthenticated == true;

        /// <summary>
        /// Obtém o identificador do usuário autenticado a partir da claim
        /// <see cref="JwtRegisteredClaimNames.Sub"/> presente no contexto HTTP.
        /// </summary>
        /// <value>
        /// Identificador do usuário autenticado ou <see langword="null"/>
        /// quando não há autenticação ou a claim não possui um valor válido.
        /// </value>
        public string? UserId
        {
            get
            {
                if (!IsAuthenticated)
                {
                    return null;
                }

                var userId = _httpContextAccessor.HttpContext?
                    .User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                return string.IsNullOrWhiteSpace(userId)
                    ? null
                    : userId;
            }
        }
    }
}
