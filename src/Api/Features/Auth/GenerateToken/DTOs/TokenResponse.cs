namespace OrderService.Api.Features.Auth.GenerateToken.DTOs
{
    /// <summary>
    /// Representa a resposta de uma autenticação
    /// realizada com sucesso.
    /// </summary>
    public sealed class TokenResponse
    {
        /// <summary>
        /// Token JWT utilizado para autenticar
        /// as requisições à API.
        /// </summary>
        public required string AccessToken { get; init; }

        /// <summary>
        /// Tipo do token de autenticação.
        /// </summary>
        public string TokenType { get; init; } = "Bearer";

        /// <summary>
        /// Quantidade de segundos até a expiração do token.
        /// </summary>
        public required int ExpiresIn { get; init; }
    }
}
